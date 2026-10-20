using System.Diagnostics;
using FluentAssertions;
using Labz.Modules.Circuits;
using Xunit.Abstractions;

namespace Labz.Modules.Circuits.Tests;

public class CalculationTests
{
    private readonly ITestOutputHelper _output;

    public CalculationTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Validator_reports_each_problem_with_the_component()
    {
        var missing = Check(new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 5))
            .Add(new ResistorComponent("R1", 100))
            .Connect("A", ("V1", "+"), ("R1", "1"))
            .Connect("B", ("V1", "-"), ("R1", "2")));
        var missingProblem = missing.Should().ContainSingle(problem => problem.Kind == CircuitProblemKind.MissingGround).Subject;
        missingProblem.Message.Should().Contain("terra");
        missingProblem.ComponentIds.Should().Contain("V1").And.Contain("R1");

        var floating = Check(new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 5))
            .Add(new ResistorComponent("R1", 100))
            .Add(new ResistorComponent("R2", 100))
            .Add(new GroundComponent("GND"))
            .Connect("N1", ("V1", "+"), ("R1", "1"))
            .Connect("0", ("V1", "-"), ("R1", "2"), ("GND", "g"))
            .Connect("X", ("R2", "1"))
            .Connect("Y", ("R2", "2")));
        var floatingProblem = floating.Should().ContainSingle(problem => problem.Kind == CircuitProblemKind.FloatingNode).Subject;
        floatingProblem.Message.Should().Contain("flutuante").And.Contain("R2");
        floatingProblem.ComponentIds.Should().Contain("R2");

        var unconnected = Check(new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 5))
            .Add(new ResistorComponent("R1", 100))
            .Add(new GroundComponent("GND"))
            .Connect("N1", ("V1", "+"), ("R1", "1"))
            .Connect("0", ("V1", "-"), ("GND", "g")));
        var pin = unconnected.Should().ContainSingle(problem => problem.Kind == CircuitProblemKind.UnconnectedPin).Subject;
        pin.Message.Should().Contain("não está ligado").And.Contain("R1.2");
        pin.ComponentIds.Should().ContainSingle().Which.Should().Be("R1");
        unconnected.Should().NotContain(problem => problem.Kind == CircuitProblemKind.FloatingNode);

        var source = Check(new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 5))
            .Add(new SwitchComponent("S1", SwitchState.Closed))
            .Add(new GroundComponent("GND"))
            .Connect("N", ("V1", "+"), ("S1", "1"))
            .Connect("0", ("V1", "-"), ("S1", "2"), ("GND", "g")));
        var shorted = source.Should().Contain(problem => problem.Kind == CircuitProblemKind.VoltageSourceShort).Subject;
        shorted.Message.Should().Contain("tensão");
        shorted.ComponentIds.Should().Contain("V1").And.Contain("S1");

        var battery = Check(new CircuitBuilder()
            .Add(new BatteryComponent("BAT1", 12, 0.1, 7, 0.5))
            .Add(new SwitchComponent("S1", SwitchState.Closed))
            .Add(new GroundComponent("GND"))
            .Connect("N", ("BAT1", "+"), ("S1", "1"))
            .Connect("0", ("BAT1", "-"), ("S1", "2"), ("GND", "g")));
        battery.Should().Contain(problem => problem.Kind == CircuitProblemKind.VoltageSourceShort
            && problem.ComponentIds.Contains("BAT1") && problem.ComponentIds.Contains("S1"));

        var panel = Check(new CircuitBuilder()
            .Add(new SolarPanelComponent("PV1", 18, 5))
            .Add(new SwitchComponent("S1", SwitchState.Closed))
            .Add(new GroundComponent("GND"))
            .Connect("N", ("PV1", "+"), ("S1", "1"))
            .Connect("0", ("PV1", "-"), ("S1", "2"), ("GND", "g")));
        panel.Should().Contain(problem => problem.Kind == CircuitProblemKind.VoltageSourceShort && problem.ComponentIds.Contains("PV1"));

        var open = Check(new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 5))
            .Add(new SwitchComponent("S1", SwitchState.Open))
            .Add(new ResistorComponent("R1", 100))
            .Add(new GroundComponent("GND"))
            .Connect("N", ("V1", "+"), ("S1", "1"))
            .Connect("M", ("S1", "2"), ("R1", "1"))
            .Connect("0", ("V1", "-"), ("R1", "2"), ("GND", "g")));
        open.Should().NotContain(problem => problem.Kind == CircuitProblemKind.VoltageSourceShort);
    }

    [Fact]
    public void Ref1_sample_matches_scenarios_and_the_fifteen_ohm_rating()
    {
        var session = SchematicSession.FromDocument(Ref1Sample.Load());
        var watch = Stopwatch.StartNew();
        var scenarioA = Solve(session);
        watch.Stop();
        _output.WriteLine($"REF-1 scenario A solve: {watch.Elapsed.TotalMilliseconds:F2} ms");
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));

        var current = 3d / 15.1;
        Relative(-scenarioA.Get("BAT1.I").Value, current);
        Relative(scenarioA.Get("energy.Wh_day").Value, 12 * current * 6);
        Relative(scenarioA.Get("energy.runtime.h").Value, 3.5 / current);
        Relative(scenarioA.Get("energy.autonomy.d").Value, 42 / (12 * current * 6));
        scenarioA.PowerRatings.Should().OnlyContain(rating => rating.Status == PowerRatingStatus.Ok && rating.Message == "OK");
        Math.Abs(scenarioA.Get("balance.P").Value).Should().BeLessThanOrEqualTo(1e-9);

        var projection = ResultProjection.From(session.State, scenarioA, 6);
        projection.Rows.Should().HaveCount(session.State.Components.Count);
        projection.Rows.Should().Contain(row => row.Id == "R1" && row.PowerTrace.Contains("R1.P · P=I²R") && row.PowerTrace.Contains("ohm.P=I2R"));
        projection.BalanceText.Should().Be("balanço de potência ✓");
        projection.ChargeEfficiency.Should().Be("Eficiência de carga: 1,0");
        projection.Cards.Single(card => card.Title == "Wh/dia").Trace.Should().Contain("energy.Wh_day");
        projection.Cards.Single(card => card.Title == "Autonomia").Text.Should().Contain("h").And.Contain("d");
        projection.SignConvention.Should().Contain("absorve");

        session.SetSwitchClosed("S1", true);
        session.SetSwitchClosed("S2", false);
        var scenarioB = Solve(session);
        var charge = 6d / 5.1;
        Relative(scenarioB.Get("BAT1.I").Value, charge);
        Relative(scenarioB.Get("energy.recharge.h").Value, 3.5 / charge);
        Relative(scenarioB.Get("energy.I_charge").Value, charge);

        session.SetSwitchClosed("S2", true);
        var scenarioC = Solve(session);
        var node = 1863d / 154d;
        Relative(scenarioC.Get("N_BAT.V").Value, node);
        Relative(-scenarioC.Get("PV1.I").Value, (18 - node) / 5);
        Relative(scenarioC.Get("BAT1.I").Value, (node - 12) / 0.1);
        Math.Abs(scenarioC.Get("balance.P").Value).Should().BeLessThanOrEqualTo(1e-9);

        session.SetSwitchClosed("S1", false);
        foreach (var resistor in session.State.Components.Where(component => component.Type == "resistor"))
        {
            session.TrySetParam(resistor.Id, "resistance", "15 Ω", out _).Should().BeTrue();
        }

        var exceeded = Solve(session);
        exceeded.PowerRatings.Should().OnlyContain(rating => rating.Status == PowerRatingStatus.Error && rating.Message == "excede a potência");
    }

    [Fact]
    public void Live_solve_runs_off_thread_debounces_and_cancels()
    {
        var calls = 0;
        var caller = Environment.CurrentManagedThreadId;
        DcSolution SolveFast(CircuitModel circuit, EnergySettings? energy, CancellationToken token, IProgress<double>? progress)
        {
            Interlocked.Increment(ref calls);
            token.ThrowIfCancellationRequested();
            progress?.Report(1);
            return new DcSolver().Solve(circuit, energy, token, progress);
        }

        var fast = new LiveSolveController(debounce: TimeSpan.FromMilliseconds(40), solve: SolveFast);
        var session = Divider();
        fast.Schedule(session.State);
        fast.Schedule(session.State);
        Wait(() => calls == 1 && fast.Solution is not null);
        calls.Should().Be(1);
        fast.SolverThreadId.Should().NotBe(caller);
        fast.Solution!.PowerBalanceOk.Should().BeTrue();

        var blocked = 0;
        var invalid = new LiveSolveController(
            debounce: TimeSpan.FromMilliseconds(10),
            solve: (_, _, _, _) =>
            {
                Interlocked.Increment(ref blocked);
                throw new InvalidOperationException("should not run");
            });
        invalid.Schedule(SchematicSession.CreateBlank("vazio", DateTimeOffset.UnixEpoch).State);
        Wait(() => invalid.BlockedMessage is not null);
        blocked.Should().Be(0);
        invalid.BlockedMessage.Should().Be("Corrija os problemas do esquema para calcular.");
        invalid.Problems.Should().Contain(problem => problem.Kind == CircuitProblemKind.MissingGround);

        using var started = new ManualResetEventSlim(false);
        var slow = new LiveSolveController(
            debounce: TimeSpan.FromMilliseconds(10),
            progressAfter: TimeSpan.FromMilliseconds(30),
            solve: (_, _, token, progress) =>
            {
                started.Set();
                while (!token.IsCancellationRequested)
                {
                    progress?.Report(0.2);
                    Thread.Sleep(10);
                }

                token.ThrowIfCancellationRequested();
                throw new InvalidOperationException("cancelled solve returned");
            });
        slow.Schedule(session.State);
        started.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue();
        var progressDeadline = DateTime.UtcNow.AddSeconds(3);
        while (!slow.ShowProgress && DateTime.UtcNow < progressDeadline)
        {
            Thread.Sleep(15);
        }

        slow.ShowProgress.Should().BeTrue(
            "running {0}, progress {1}, blocked {2}, problems {3}",
            slow.IsRunning,
            slow.Progress,
            slow.BlockedMessage,
            slow.Problems.Count);
        var cancel = Stopwatch.StartNew();
        slow.Cancel();
        Wait(() => !slow.IsRunning);
        cancel.Stop();
        cancel.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    private static IReadOnlyList<CircuitProblem> Check(CircuitBuilder builder) =>
        CircuitDiagnostics.Check(builder.Build());

    private static DcSolution Solve(SchematicSession session) =>
        new DcSolver().Solve(CircuitMapper.ToCircuit(session.State), CircuitMapper.ToEnergy(session.State));

    private static SchematicSession Divider()
    {
        var session = SchematicSession.CreateBlank("divisor", DateTimeOffset.UnixEpoch);
        var source = session.Place("dc_voltage_source", 0, 0);
        var first = session.Place("resistor", 120, 0);
        var second = session.Place("resistor", 240, 0);
        var ground = session.Place("ground", 120, 120);
        session.TrySetParam(source, "voltage", "10 V", out _);
        session.TrySetParam(first, "resistance", "1 kΩ", out _);
        session.TrySetParam(second, "resistance", "1 kΩ", out _);
        session.ConnectPins(source, "+", first, "1");
        session.ConnectPins(first, "2", second, "1");
        session.ConnectPins(second, "2", ground, "g");
        session.ConnectPins(source, "-", ground, "g");
        return session;
    }

    private static void Relative(double actual, double expected)
    {
        var error = Math.Abs(actual - expected) / Math.Abs(expected);
        error.Should().BeLessThanOrEqualTo(1e-6);
    }

    private static void Wait(Func<bool> condition)
    {
        var start = DateTime.UtcNow;
        while (!condition())
        {
            if (DateTime.UtcNow - start > TimeSpan.FromSeconds(2))
            {
                throw new TimeoutException("Condition was not reached.");
            }

            Thread.Sleep(15);
        }
    }
}
