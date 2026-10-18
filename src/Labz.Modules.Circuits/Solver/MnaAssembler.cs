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

    public static SolvedCircuit Solve(Topology topology, IReadOnlySet<string> ledsOn, CancellationToken cancellationToken)
    {
        var diodes = topology.Circuit.Components.OfType<DiodeComponent>().ToArray();
        if (diodes.Length == 0)
        {
            return SolveLinear(topology, ledsOn, linearization: null, cancellationToken);
        }

        var linearization = new double[diodes.Length];
        for (var i = 0; i < diodes.Length; i++)
        {
            var model = diodes[i].Model;
            var nVt = model.EmissionCoefficient * DiodePhysics.ThermalVoltage;
            linearization[i] = nVt * Math.Log((1e-3 / model.SaturationCurrent) + 1);
        }

        SolvedCircuit? solved = null;
        for (var iteration = 0; iteration < NewtonLimit; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            solved = SolveLinear(topology, ledsOn, linearization, cancellationToken);
            var converged = true;
            for (var i = 0; i < diodes.Length; i++)
            {
                var proposed = JunctionVoltage(topology, solved, diodes[i]);
                var nVt = diodes[i].Model.EmissionCoefficient * DiodePhysics.ThermalVoltage;
                if (Math.Abs(proposed - linearization[i]) > 1e-12)
                {
                    converged = false;
                }

                linearization[i] = LimitDiodeStep(linearization[i], proposed, nVt);
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
        CancellationToken cancellationToken)
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
            return new SolvedCircuit(topology, system.Solve(), currents);
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

    private static double LimitDiodeStep(double current, double proposed, double nVt)
    {
        var delta = proposed - current;
        var maxStep = delta >= 0 ? Math.Max(2 * nVt, 0.05) : Math.Max(10 * nVt, 0.5);
        var updated = Math.Abs(delta) <= maxStep ? proposed : current + (Math.Sign(delta) * maxStep);
        var cap = 40 * nVt;
        if (updated > cap)
        {
            return cap;
        }

        return updated < -2 ? -2 : updated;
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

        public double[] Solve() => DenseLinearSolver.Solve(_matrix, _rhs);

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
