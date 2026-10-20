using Labz.Core.Engine;
using Labz.Core.Units;

namespace Labz.Modules.Circuits;

/// <summary>
/// DC operating point. Component current is positive into the positive pin, so a source that delivers power has negative power.
/// The solve method accepts a cancellation token and progress report so the UI can host it off the UI thread.
/// </summary>
public sealed class DcSolver
{
    public const double PowerBalanceToleranceWatts = 1e-9;

    private const double StateCurrentTolerance = 1e-9;

    private const double DirectionTolerance = 1e-8;

    private const int LedIterationLimit = 48;

    private readonly FormulaRegistry _formulas;

    public DcSolver(FormulaRegistry? formulas = null)
    {
        _formulas = formulas ?? StandardFormulas.Create();
    }

    public DcSolution Solve(
        CircuitModel circuit,
        EnergySettings? energy = null,
        CancellationToken cancellationToken = default,
        IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(circuit);
        energy?.EnsureValid();
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(0);

        var topology = Topology.Build(circuit);
        var componentIds = circuit.Components.Select(component => component.Id).Order(StringComparer.Ordinal).ToArray();
        var leds = circuit.Components.OfType<LedComponent>().ToArray();
        var ledIds = leds.Select(led => led.Id).ToHashSet(StringComparer.Ordinal);

        if (topology.TryGetVoltageLoop(new HashSet<string>(StringComparer.Ordinal), out var structuralLoop))
        {
            throw new VoltageSourceLoopException(Strings.VoltageSourceLoop(Join(structuralLoop)), structuralLoop.ToArray());
        }

        if (topology.TryGetFloating(ledIds, out var floatingNodes, out var floatingComponents))
        {
            throw new FloatingNodeException(
                Strings.FloatingNode(Join(floatingNodes), Join(floatingComponents)),
                floatingNodes.ToArray(),
                floatingComponents.ToArray());
        }

        var ledsOn = new HashSet<string>(ledIds, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        SolvedCircuit? solved = null;

        for (var iteration = 0; iteration < LedIterationLimit; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (bandStart, bandEnd) = ProgressBand(iteration);

            var key = string.Join('\n', ledsOn.Order(StringComparer.Ordinal));
            if (!seen.Add(key))
            {
                throw new DcSolverConvergenceException(Strings.NoConvergence(Join(componentIds)), componentIds);
            }

            if (topology.TryGetVoltageLoop(ledsOn, out var loop))
            {
                var involvedLeds = loop.Where(ledsOn.Contains).ToArray();
                if (involvedLeds.Length == 0)
                {
                    throw new VoltageSourceLoopException(Strings.VoltageSourceLoop(Join(loop)), loop.ToArray());
                }

                foreach (var ledId in involvedLeds)
                {
                    ledsOn.Remove(ledId);
                }

                continue;
            }

            if (topology.TryGetFloating(ledsOn, out var nodes, out var floating))
            {
                throw new FloatingNodeException(
                    Strings.FloatingNode(Join(nodes), Join(floating)),
                    nodes.ToArray(),
                    floating.ToArray());
            }

            var candidate = MnaAssembler.Solve(
                topology,
                ledsOn,
                cancellationToken,
                progress,
                bandStart,
                bandEnd);
            if (!UpdateLedStates(topology, leds, ledsOn, candidate))
            {
                solved = candidate;
                break;
            }
        }

        if (solved is null)
        {
            throw new DcSolverConvergenceException(Strings.NoConvergence(Join(componentIds)), componentIds);
        }

        var solution = BuildSolution(topology, solved, ledsOn, componentIds, energy);
        if (!solution.PowerBalanceOk)
        {
            throw new DcSolverConvergenceException(Strings.PowerBalance(Join(componentIds)), componentIds);
        }

        progress?.Report(1);
        return solution;
    }

    private bool UpdateLedStates(
        Topology topology,
        IReadOnlyList<LedComponent> leds,
        HashSet<string> ledsOn,
        SolvedCircuit solved)
    {
        var off = new List<string>();
        var on = new List<string>();
        foreach (var led in leds)
        {
            if (ledsOn.Contains(led.Id))
            {
                if (solved.Current(led.Id) < -StateCurrentTolerance)
                {
                    off.Add(led.Id);
                }
            }
            else
            {
                var anode = solved.Voltage(topology.Pin(led.Id, "A"));
                var cathode = solved.Voltage(topology.Pin(led.Id, "K"));
                if (anode - cathode > led.ForwardVoltage + StateCurrentTolerance)
                {
                    var trial = new HashSet<string>(ledsOn, StringComparer.Ordinal) { led.Id };
                    if (topology.TryGetVoltageLoop(trial, out var loop) && loop.Contains(led.Id))
                    {
                        throw new OvercurrentException(
                            Strings.Overcurrent(led.Id, Join(loop)),
                            loop.ToArray());
                    }

                    on.Add(led.Id);
                }
            }
        }

        if (off.Count == 0 && on.Count == 0)
        {
            return false;
        }

        foreach (var ledId in off)
        {
            ledsOn.Remove(ledId);
        }

        foreach (var ledId in on)
        {
            ledsOn.Add(ledId);
        }

        return true;
    }

    private DcSolution BuildSolution(
        Topology topology,
        SolvedCircuit solved,
        IReadOnlySet<string> ledsOn,
        string[] componentIds,
        EnergySettings? energy)
    {
        var results = new ResultStore();
        var branches = new Dictionary<string, Branch>(StringComparer.Ordinal);
        var parts = new Dictionary<string, Branch>(StringComparer.Ordinal);

        foreach (var (netId, node) in topology.UserNetNodes)
        {
            results.Add(Result($"{netId}.V", solved.Voltage(node), "V", "mna.dc", componentIds));
        }

        foreach (var (index, nodeId) in topology.NodeIds.Select((id, nodeIndex) => (nodeIndex, id)))
        {
            if (topology.UserNetNodes.ContainsKey(nodeId))
            {
                continue;
            }

            results.Add(Result($"{nodeId}.V", solved.Voltage(index), "V", "mna.dc", componentIds));
        }

        foreach (var component in topology.Circuit.Components)
        {
            var branch = BranchOf(topology, solved, ledsOn, component);
            branches[component.Id] = branch;
            results.Add(Result($"{component.Id}.V", branch.Voltage, "V", "mna.dc", componentIds));
            results.Add(Result($"{component.Id}.I", branch.Current, "A", "mna.dc", componentIds));
            var powerFormula = component is ResistorComponent ? "ohm.P=I2R" : "ohm.P=VI";
            var powerInputs = component switch
            {
                ResistorComponent => new[] { $"{component.Id}.I", $"{component.Id}.resistance" },
                _ => new[] { $"{component.Id}.V", $"{component.Id}.I" },
            };
            results.Add(Result($"{component.Id}.P", branch.Power, "W", powerFormula, powerInputs));
            AddSeriesParts(topology, solved, component, parts, results, componentIds);
        }

        var loadPower = branches
            .Where(pair => IsLoad(topology.Circuit.Components.First(component => component.Id == pair.Key)))
            .Sum(pair => pair.Value.Power);
        var passiveIds = topology.Circuit.Components.Where(IsLoad).Select(component => component.Id).ToArray();
        results.Add(Result("load.P", loadPower, "W", "mna.dc", passiveIds));

        var balance = branches.Values.Sum(branch => branch.Power);
        results.Add(Result("balance.P", balance, "W", "mna.dc", componentIds));
        var ratings = AddPowerRatings(topology, branches, results);
        AddEnergy(topology, branches, parts, energy, results);

        return new DcSolution(results, Math.Abs(balance) <= PowerBalanceToleranceWatts, ratings);
    }

    private Branch BranchOf(Topology topology, SolvedCircuit solved, IReadOnlySet<string> ledsOn, CircuitComponent component)
    {
        switch (component)
        {
            case ResistorComponent resistor:
            {
                var voltage = VoltageAcross(solved, topology, resistor.Id, "1", "2");
                var current = voltage / resistor.ResistanceOhms;
                return new Branch(voltage, current, PowerI2R(current, resistor.ResistanceOhms));
            }
            case CapacitorComponent capacitor:
                return new Branch(VoltageAcross(solved, topology, capacitor.Id, "1", "2"), 0, 0);
            case InductorComponent inductor:
            {
                var voltage = VoltageAcross(solved, topology, inductor.Id, "1", "2");
                var current = solved.Current(inductor.Id);
                return new Branch(voltage, current, PowerVI(voltage, current));
            }
            case DiodeComponent diode:
            {
                var voltage = VoltageAcross(solved, topology, diode.Id, "A", "K");
                var current = MnaAssembler.ShockleyCurrent(diode.Model, MnaAssembler.JunctionVoltage(topology, solved, diode));
                return new Branch(voltage, current, PowerVI(voltage, current));
            }
            case LedComponent led when ledsOn.Contains(led.Id):
            {
                var voltage = VoltageAcross(solved, topology, led.Id, "A", "K");
                var current = solved.Current(led.Id);
                return new Branch(voltage, current, PowerVI(voltage, current));
            }
            case LedComponent led:
                return new Branch(VoltageAcross(solved, topology, led.Id, "A", "K"), 0, 0);
            case DcVoltageSourceComponent source:
            {
                var voltage = VoltageAcross(solved, topology, source.Id, "+", "-");
                var current = solved.Current(source.Id);
                return new Branch(voltage, current, PowerVI(voltage, current));
            }
            case BatteryComponent battery:
            {
                var voltage = VoltageAcross(solved, topology, battery.Id, "+", "-");
                var current = solved.Current(battery.Id);
                return new Branch(voltage, current, PowerVI(voltage, current));
            }
            case SolarPanelComponent panel:
            {
                var voltage = VoltageAcross(solved, topology, panel.Id, "+", "-");
                var current = solved.Current(panel.Id);
                return new Branch(voltage, current, PowerVI(voltage, current));
            }
            case SwitchComponent { State: SwitchState.Closed } closed:
            {
                var voltage = VoltageAcross(solved, topology, closed.Id, "1", "2");
                var current = solved.Current(closed.Id);
                return new Branch(voltage, current, PowerVI(voltage, current));
            }
            case SwitchComponent open:
                return new Branch(VoltageAcross(solved, topology, open.Id, "1", "2"), 0, 0);
            case GroundComponent:
                return new Branch(0, 0, 0);
            default:
                throw new InvalidOperationException($"Unsupported component '{component.TypeName}'.");
        }
    }

    private void AddSeriesParts(
        Topology topology,
        SolvedCircuit solved,
        CircuitComponent component,
        Dictionary<string, Branch> parts,
        ResultStore results,
        string[] componentIds)
    {
        switch (component)
        {
            case BatteryComponent battery:
                AddEmfAndResistance(battery.Id, battery.NominalVoltage, battery.InternalResistance, solved, parts, results, componentIds);
                break;
            case SolarPanelComponent panel:
                AddEmfAndResistance(panel.Id, panel.OpenCircuitVoltage, panel.SeriesResistance, solved, parts, results, componentIds);
                break;
        }
    }

    private void AddEmfAndResistance(
        string id,
        double emfVoltage,
        double resistance,
        SolvedCircuit solved,
        Dictionary<string, Branch> parts,
        ResultStore results,
        string[] componentIds)
    {
        var current = solved.Current(id);
        var emf = new Branch(emfVoltage, current, PowerVI(emfVoltage, current));
        var resistorVoltage = current * resistance;
        var resistor = new Branch(resistorVoltage, current, resistance == 0 ? 0 : PowerI2R(current, resistance));
        parts[id + ".emf"] = emf;
        parts[id + ".R"] = resistor;
        results.Add(Result($"{id}.emf.V", emf.Voltage, "V", "mna.dc", componentIds));
        results.Add(Result($"{id}.emf.I", emf.Current, "A", "mna.dc", componentIds));
        results.Add(Result($"{id}.emf.P", emf.Power, "W", "ohm.P=VI", [$"{id}.emf.V", $"{id}.emf.I"]));
        var resistanceId = IdHasBattery(id, solved) ? "Rint" : "Rs";
        results.Add(Result($"{id}.{resistanceId}.V", resistor.Voltage, "V", "mna.dc", componentIds));
        results.Add(Result($"{id}.{resistanceId}.I", resistor.Current, "A", "mna.dc", componentIds));
        var parameterName = resistanceId == "Rint" ? "internalResistance" : "seriesResistance";
        results.Add(Result($"{id}.{resistanceId}.P", resistor.Power, "W", "ohm.P=I2R", [$"{id}.{resistanceId}.I", $"{id}.{parameterName}"]));
    }

    private static bool IdHasBattery(string id, SolvedCircuit solved) =>
        solved.Topology.Circuit.Components.OfType<BatteryComponent>().Any(battery => battery.Id == id);

    private List<PowerRatingFinding> AddPowerRatings(
        Topology topology,
        Dictionary<string, Branch> branches,
        ResultStore results)
    {
        var findings = new List<PowerRatingFinding>();
        foreach (var resistor in topology.Circuit.Components.OfType<ResistorComponent>())
        {
            var power = branches[resistor.Id].Power;
            var ratio = _formulas.EvaluateWorking(
                "power.rating",
                Quantity.Of(power, KnownUnits.Watt),
                Quantity.Of(resistor.PowerRatingWatts, KnownUnits.Watt));
            // 50% and 100% belong to the warning band. A few ulps of solver noise must not cross the line.
            const double boundaryNoise = 1e-9;
            var status = ratio < 0.5 - boundaryNoise
                ? PowerRatingStatus.Ok
                : ratio <= 1 + boundaryNoise
                    ? PowerRatingStatus.Warning
                    : PowerRatingStatus.Error;
            var message = status switch
            {
                PowerRatingStatus.Ok => Strings.PowerRatingOk,
                PowerRatingStatus.Warning => Strings.PowerRatingWarning,
                PowerRatingStatus.Error => Strings.PowerRatingExceeded,
                _ => throw new InvalidOperationException($"Unsupported rating status {status}."),
            };
            findings.Add(new PowerRatingFinding(resistor.Id, status, message, power, resistor.PowerRatingWatts, ratio));
            results.Add(Result(
                $"{resistor.Id}.rating",
                ratio,
                "1",
                "power.rating",
                [$"{resistor.Id}.P", $"{resistor.Id}.powerRating"]));
        }

        return findings;
    }

    private void AddEnergy(
        Topology topology,
        Dictionary<string, Branch> branches,
        Dictionary<string, Branch> parts,
        EnergySettings? energy,
        ResultStore results)
    {
        var batteries = topology.Circuit.Components.OfType<BatteryComponent>().ToArray();
        foreach (var battery in batteries)
        {
            var prefix = batteries.Length == 1 ? "energy" : battery.Id + ".energy";
            var capacity = Quantity.Of(battery.CapacityAmpereHours, KnownUnits.AmpereHour);
            var depth = Quantity.Of(battery.UsableDepthOfDischarge, KnownUnits.Dimensionless);
            var nominal = Quantity.Of(battery.NominalVoltage, KnownUnits.Volt);
            var usableAh = _formulas.EvaluateWorking("energy.usable_Ah", capacity, depth);
            var usableWh = _formulas.EvaluateWorking("energy.usable_Wh", capacity, depth, nominal);
            results.Add(Result(
                prefix + ".usable.Ah",
                usableAh,
                "Ah",
                "energy.usable_Ah",
                [$"{battery.Id}.capacity", $"{battery.Id}.usableDepthOfDischarge"]));
            results.Add(Result(
                prefix + ".usable.Wh",
                usableWh,
                "Wh",
                "energy.usable_Wh",
                [$"{battery.Id}.capacity", $"{battery.Id}.usableDepthOfDischarge", $"{battery.Id}.nominalVoltage"]));

            var terminalCurrent = branches[battery.Id].Current;
            var emfPower = parts[battery.Id + ".emf"].Power;
            if (terminalCurrent < -DirectionTolerance)
            {
                var dischargeCurrent = -terminalCurrent;
                var drawn = -emfPower;
                results.Add(Result(prefix + ".P_source", drawn, "W", "ohm.P=VI", [$"{battery.Id}.nominalVoltage", $"{battery.Id}.I"]));
                var runtime = _formulas.EvaluateWorking(
                    "energy.runtime_h",
                    capacity,
                    depth,
                    Quantity.Of(dischargeCurrent, KnownUnits.Ampere));
                results.Add(Result(
                    prefix + ".runtime.h",
                    runtime,
                    "h",
                    "energy.runtime_h",
                    [$"{battery.Id}.capacity", $"{battery.Id}.usableDepthOfDischarge", $"{battery.Id}.I"]));

                if (energy?.LoadHoursPerDay is double hours)
                {
                    var whPerDay = _formulas.EvaluateWorking(
                        "energy.Wh_day",
                        Quantity.Of(drawn, KnownUnits.Watt),
                        Quantity.Of(hours, KnownUnits.Hour));
                    results.Add(Result(
                        prefix + ".Wh_day",
                        whPerDay,
                        "Wh/day",
                        "energy.Wh_day",
                        [$"{battery.Id}.emf.P", "usage.loadHoursPerDay"]));
                    if (whPerDay > 0)
                    {
                        var autonomy = _formulas.EvaluateWorking(
                            "energy.autonomy_d",
                            Quantity.Of(usableWh, KnownUnits.WattHour),
                            Quantity.Of(whPerDay, KnownUnits.WattHourPerDay));
                        results.Add(Result(
                            prefix + ".autonomy.d",
                            autonomy,
                            "d",
                            "energy.autonomy_d",
                            [prefix + ".usable.Wh", prefix + ".Wh_day"]));
                    }
                }
            }
            else if (terminalCurrent > DirectionTolerance && energy is not null)
            {
                results.Add(Result(prefix + ".I_charge", terminalCurrent, "A", "mna.dc", [$"{battery.Id}.I"]));
                results.Add(Result(prefix + ".P_stored", emfPower, "W", "ohm.P=VI", [$"{battery.Id}.emf.V", $"{battery.Id}.emf.I"]));
                var recharge = _formulas.EvaluateWorking(
                    "energy.recharge_h",
                    capacity,
                    depth,
                    Quantity.Of(terminalCurrent, KnownUnits.Ampere),
                    Quantity.Of(energy.ChargeEfficiency, KnownUnits.Dimensionless));
                results.Add(Result(
                    prefix + ".recharge.h",
                    recharge,
                    "h",
                    "energy.recharge_h",
                    [$"{battery.Id}.capacity", $"{battery.Id}.usableDepthOfDischarge", $"{battery.Id}.I", "engine.chargeEfficiency"]));

                if (energy.PeakSunHours is double peakSunHours)
                {
                    var chargeEnergy = _formulas.EvaluateWorking(
                        "energy.charge_Wh_day",
                        Quantity.Of(emfPower, KnownUnits.Watt),
                        Quantity.Of(peakSunHours, KnownUnits.Hour));
                    results.Add(Result(
                        prefix + ".charge.Wh_day",
                        chargeEnergy,
                        "Wh/day",
                        "energy.charge_Wh_day",
                        [$"{battery.Id}.emf.P", "usage.peakSunHours"]));
                }
            }
        }
    }

    private double PowerVI(double voltage, double current) =>
        _formulas.EvaluateWorking("ohm.P=VI", Quantity.Of(voltage, KnownUnits.Volt), Quantity.Of(current, KnownUnits.Ampere));

    private double PowerI2R(double current, double resistance) =>
        _formulas.EvaluateWorking("ohm.P=I2R", Quantity.Of(current, KnownUnits.Ampere), Quantity.Of(resistance, KnownUnits.Ohm));

    private static double VoltageAcross(SolvedCircuit solved, Topology topology, string id, string positive, string negative) =>
        solved.Voltage(topology.Pin(id, positive)) - solved.Voltage(topology.Pin(id, negative));

    private static bool IsLoad(CircuitComponent component) =>
        component is ResistorComponent or CapacitorComponent or InductorComponent or DiodeComponent or LedComponent;

    private static EngineResult Result(string id, double value, string unit, string formulaId, IReadOnlyList<string> inputIds) =>
        new(id, value, unit, formulaId, inputIds);

    private static (double Start, double End) ProgressBand(int iteration)
    {
        // The first attempt owns most of the bar, so a linear circuit reports
        // factorization progress instead of a single 1/48 tick.
        if (iteration == 0)
        {
            return (0.05, 0.9);
        }

        var start = 0.9 + (0.08 * (iteration - 1) / LedIterationLimit);
        return (start, Math.Min(0.98, start + (0.08 / LedIterationLimit)));
    }

    private static string Join(IEnumerable<string> ids)
    {
        var ordered = ids.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return ordered.Length == 0 ? "—" : string.Join(", ", ordered);
    }

    private readonly record struct Branch(double Voltage, double Current, double Power);
}
