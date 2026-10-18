using Labz.Core.Engine;

namespace Labz.Modules.Circuits;

internal sealed class SolvedCircuit
{
    private readonly Dictionary<string, int> _currents;

    public SolvedCircuit(Topology topology, double[] solution, Dictionary<string, int> currents)
    {
        Topology = topology;
        Solution = solution;
        _currents = currents;
    }

    public Topology Topology { get; }

    public double[] Solution { get; }

    public double Voltage(int node) => node == 0 ? 0 : Solution[node - 1];

    public double Current(string componentId) => Solution[_currents[componentId]];

    public bool HasCurrent(string componentId) => _currents.ContainsKey(componentId);
}

/// <summary>
/// Stamps modified nodal analysis. Positive source current enters the positive terminal (passive sign).
/// A closed switch and a DC inductor are ideal 0 V sources; Ron and Roff are not stamped.
/// </summary>
internal static class MnaAssembler
{
    private const int NewtonLimit = 80;

    public static SolvedCircuit Solve(
        Topology topology,
        IReadOnlySet<string> ledsOn,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null,
        double progressStart = 0,
        double progressEnd = 1)
    {
        var diodes = topology.Circuit.Components.OfType<DiodeComponent>().ToArray();
        if (diodes.Length == 0)
        {
            return SolveLinear(topology, ledsOn, linearization: null, cancellationToken, progress, progressStart, progressEnd);
        }

        var linearization = new double[diodes.Length];
        for (var i = 0; i < diodes.Length; i++)
        {
            var model = diodes[i].Model;
            var nVt = model.EmissionCoefficient * DiodePhysics.ThermalVoltage;
            linearization[i] = nVt * Math.Log((1e-3 / model.SaturationCurrent) + 1);
        }

        SolvedCircuit? solved = null;
        var span = progressEnd - progressStart;
        for (var iteration = 0; iteration < NewtonLimit; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stepStart = progressStart + (span * iteration / NewtonLimit);
            var stepEnd = progressStart + (span * (iteration + 1) / NewtonLimit);
            solved = SolveLinear(topology, ledsOn, linearization, cancellationToken, progress, stepStart, stepEnd);
            var converged = true;
            for (var i = 0; i < diodes.Length; i++)
            {
                var proposed = JunctionVoltage(topology, solved, diodes[i]);
                var nVt = diodes[i].Model.EmissionCoefficient * DiodePhysics.ThermalVoltage;
                if (Math.Abs(proposed - linearization[i]) > 1e-12)
                {
                    converged = false;
                }

                linearization[i] = LimitJunctionVoltage(
                    linearization[i],
                    proposed,
                    nVt,
                    diodes[i].Model.SaturationCurrent);
            }

            if (converged)
            {
                return solved;
            }
        }

        throw new DcSolverConvergenceException(
            Strings.NoConvergence(Join(topology.Circuit.Components.Select(component => component.Id))),
            topology.Circuit.Components.Select(component => component.Id).ToArray());
    }

    public static double JunctionVoltage(Topology topology, SolvedCircuit solved, DiodeComponent diode)
    {
        var cathode = topology.Pin(diode.Id, "K");
        var junction = topology.TryInternal(diode.Id, "junction", out var internalNode)
            ? internalNode
            : topology.Pin(diode.Id, "A");
        return solved.Voltage(junction) - solved.Voltage(cathode);
    }

    public static double ShockleyCurrent(DiodeModel model, double junctionVoltage)
    {
        EvaluateShockley(model, junctionVoltage, out var current, out _);
        return current;
    }

    private static SolvedCircuit SolveLinear(
        Topology topology,
        IReadOnlySet<string> ledsOn,
        double[]? linearization,
        CancellationToken cancellationToken,
        IProgress<double>? progress,
        double progressStart,
        double progressEnd)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var extras = CountVoltageUnknowns(topology, ledsOn);
        var system = new MnaSystem(topology.NodeIds.Count, extras);
        var currents = new Dictionary<string, int>(StringComparer.Ordinal);
        var diodes = topology.Circuit.Components.OfType<DiodeComponent>().ToArray();

        foreach (var component in topology.Circuit.Components)
        {
            switch (component)
            {
                case ResistorComponent resistor:
                    system.StampConductance(
                        topology.Pin(resistor.Id, "1"),
                        topology.Pin(resistor.Id, "2"),
                        1 / resistor.ResistanceOhms);
                    break;
                case DiodeComponent diode:
                    StampDiode(topology, system, diode, diodes, linearization);
                    break;
                case DcVoltageSourceComponent source:
                    currents[source.Id] = system.StampVoltageSource(
                        topology.Pin(source.Id, "+"),
                        topology.Pin(source.Id, "-"),
                        source.Voltage);
                    break;
                case BatteryComponent battery:
                    currents[battery.Id] = StampSeriesSource(
                        topology,
                        system,
                        battery.Id,
                        battery.NominalVoltage,
                        battery.InternalResistance);
                    break;
                case SolarPanelComponent panel:
                    currents[panel.Id] = StampSeriesSource(
                        topology,
                        system,
                        panel.Id,
                        panel.OpenCircuitVoltage,
                        panel.SeriesResistance);
                    break;
                case LedComponent led when ledsOn.Contains(led.Id):
                    currents[led.Id] = system.StampVoltageSource(
                        topology.Pin(led.Id, "A"),
                        topology.Pin(led.Id, "K"),
                        led.ForwardVoltage);
                    break;
                case SwitchComponent { State: SwitchState.Closed } closed:
                    // Ideal short. Ron is intentionally ignored; it belongs to the SPICE path.
                    currents[closed.Id] = system.StampVoltageSource(
                        topology.Pin(closed.Id, "1"),
                        topology.Pin(closed.Id, "2"),
                        0);
                    break;
                case InductorComponent inductor:
                    currents[inductor.Id] = system.StampVoltageSource(
                        topology.Pin(inductor.Id, "1"),
                        topology.Pin(inductor.Id, "2"),
                        0);
                    break;
            }
        }

        system.EnsureComplete();
        try
        {
            return new SolvedCircuit(
                topology,
                system.Solve(cancellationToken, progress, progressStart, progressEnd),
                currents);
        }
        catch (SingularLinearSystemException)
        {
            var ids = topology.Circuit.Components.Select(component => component.Id).ToArray();
            throw new SingularCircuitException(Strings.Singular(Join(ids)), ids);
        }
    }

    private static int StampSeriesSource(Topology topology, MnaSystem system, string id, double volts, double resistance)
    {
        var positive = topology.Pin(id, "+");
        var negative = topology.Pin(id, "-");
        if (resistance > 0)
        {
            var series = topology.TryInternal(id, "series", out var node)
                ? node
                : throw new InvalidOperationException($"Missing series node for '{id}'.");
            system.StampConductance(positive, series, 1 / resistance);
            return system.StampVoltageSource(series, negative, volts);
        }

        return system.StampVoltageSource(positive, negative, volts);
    }

    private static void StampDiode(
        Topology topology,
        MnaSystem system,
        DiodeComponent diode,
        DiodeComponent[] diodes,
        double[]? linearization)
    {
        var index = Array.IndexOf(diodes, diode);
        var vd = linearization is null ? 0 : linearization[index];
        var anode = topology.Pin(diode.Id, "A");
        var cathode = topology.Pin(diode.Id, "K");
        var junction = anode;
        if (diode.Model.SeriesResistance > 0)
        {
            junction = topology.TryInternal(diode.Id, "junction", out var node)
                ? node
                : throw new InvalidOperationException($"Missing junction node for '{diode.Id}'.");
            system.StampConductance(anode, junction, 1 / diode.Model.SeriesResistance);
        }

        EvaluateShockley(diode.Model, vd, out var current, out var conductance);
        var equivalent = current - (conductance * vd);
        system.StampCompanion(junction, cathode, conductance, equivalent);
    }

    private static void EvaluateShockley(DiodeModel model, double junctionVoltage, out double current, out double conductance)
    {
        var nVt = model.EmissionCoefficient * DiodePhysics.ThermalVoltage;
        var argument = junctionVoltage / nVt;
        if (argument > 40)
        {
            argument = 40;
        }
        else if (argument < -40)
        {
            argument = -40;
        }

        var exponential = Math.Exp(argument);
        current = model.SaturationCurrent * (exponential - 1);
        conductance = Math.Max(0, model.SaturationCurrent * exponential / nVt);
    }

    /// <summary>
    /// SPICE pnjlim. Large forward jumps are logged so the exponential stays finite.
    /// Reverse bias is not clamped: a diode at -100 V must be allowed to sit there.
    /// </summary>
    private static double LimitJunctionVoltage(double previous, double proposed, double nVt, double saturationCurrent)
    {
        if (nVt <= 0 || saturationCurrent <= 0 || !double.IsFinite(proposed))
        {
            return previous;
        }

        var vcrit = nVt * Math.Log(nVt / (Math.Sqrt(2) * saturationCurrent));
        if (proposed > vcrit && Math.Abs(proposed - previous) > (2 * nVt))
        {
            if (previous > 0)
            {
                var argument = 1 + ((proposed - previous) / nVt);
                return argument > 0 ? previous + (nVt * Math.Log(argument)) : vcrit;
            }

            var ratio = proposed / nVt;
            return ratio > 0 ? nVt * Math.Log(ratio) : vcrit;
        }

        return proposed;
    }

    private static int CountVoltageUnknowns(Topology topology, IReadOnlySet<string> ledsOn)
    {
        var count = 0;
        foreach (var component in topology.Circuit.Components)
        {
            switch (component)
            {
                case DcVoltageSourceComponent:
                case BatteryComponent:
                case SolarPanelComponent:
                case InductorComponent:
                    count++;
                    break;
                case SwitchComponent { State: SwitchState.Closed }:
                    count++;
                    break;
                case LedComponent led when ledsOn.Contains(led.Id):
                    count++;
                    break;
            }
        }

        return count;
    }

    private static string Join(IEnumerable<string> ids)
    {
        var ordered = ids.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return ordered.Length == 0 ? "—" : string.Join(", ", ordered);
    }

    private sealed class MnaSystem
    {
        private readonly double[,] _matrix;
        private readonly double[] _rhs;
        private readonly int _voltageUnknowns;
        private readonly int _expectedExtras;
        private int _extras;

        public MnaSystem(int nodeCount, int extraCount)
        {
            _voltageUnknowns = nodeCount - 1;
            _expectedExtras = extraCount;
            var size = _voltageUnknowns + extraCount;
            _matrix = new double[size, size];
            _rhs = new double[size];
        }

        public int StampVoltageSource(int positive, int negative, double volts)
        {
            var unknown = _voltageUnknowns + _extras;
            _extras++;
            AddKcl(positive, unknown, 1);
            AddKcl(negative, unknown, -1);
            AddConstraint(unknown, positive, 1);
            AddConstraint(unknown, negative, -1);
            _rhs[unknown] = volts;
            return unknown;
        }

        public void StampConductance(int left, int right, double conductance)
        {
            AddVoltage(left, left, conductance);
            AddVoltage(left, right, -conductance);
            AddVoltage(right, left, -conductance);
            AddVoltage(right, right, conductance);
        }

        public void StampCompanion(int from, int to, double conductance, double equivalentCurrent)
        {
            StampConductance(from, to, conductance);
            AddRhs(from, -equivalentCurrent);
            AddRhs(to, equivalentCurrent);
        }

        public void EnsureComplete()
        {
            if (_extras != _expectedExtras)
            {
                throw new InvalidOperationException("The MNA stamp did not match the voltage-source count.");
            }
        }

        public double[] Solve(
            CancellationToken cancellationToken,
            IProgress<double>? progress,
            double progressStart,
            double progressEnd) =>
            DenseLinearSolver.Solve(_matrix, _rhs, cancellationToken, progress, progressStart, progressEnd);

        private void AddVoltage(int rowNode, int columnNode, double value)
        {
            if (rowNode == 0 || columnNode == 0 || value == 0)
            {
                return;
            }

            _matrix[rowNode - 1, columnNode - 1] += value;
        }

        private void AddKcl(int node, int unknown, double value)
        {
            if (node == 0 || value == 0)
            {
                return;
            }

            _matrix[node - 1, unknown] += value;
        }

        private void AddConstraint(int unknown, int node, double value)
        {
            if (node == 0 || value == 0)
            {
                return;
            }

            _matrix[unknown, node - 1] += value;
        }

        private void AddRhs(int node, double value)
        {
            if (node == 0 || value == 0)
            {
                return;
            }

            _rhs[node - 1] += value;
        }
    }
}
