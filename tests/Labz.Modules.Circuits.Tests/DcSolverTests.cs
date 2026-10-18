using FluentAssertions;
using Labz.Core.Units;
using Labz.Modules.Circuits;

namespace Labz.Modules.Circuits.Tests;

public class DcSolverTests
{
    private readonly DcSolver _solver = new();

    [Fact]
    public void Voltage_divider_matches_ohms_law()
    {
        var circuit = new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 10))
            .Add(new ResistorComponent("R1", 1000))
            .Add(new ResistorComponent("R2", 1000))
            .Add(new GroundComponent("GND"))
            .Connect("N1", ("V1", "+"), ("R1", "1"))
            .Connect("N2", ("R1", "2"), ("R2", "1"))
            .Connect("0", ("V1", "-"), ("R2", "2"), ("GND", "g"))
            .Build();

        var solution = _solver.Solve(circuit);
        solution.Get("N2.V").Value.Should().BeApproximately(5, 1e-9);
        solution.Get("R1.I").Value.Should().BeApproximately(0.005, 1e-12);
        solution.Get("R2.I").Value.Should().BeApproximately(0.005, 1e-12);
        solution.Get("V1.I").Value.Should().BeApproximately(-0.005, 1e-12);
        solution.Get("V1.P").Value.Should().BeApproximately(-0.05, 1e-12);
        solution.Get("R1.P").Value.Should().BeApproximately(0.025, 1e-12);
        Math.Abs(solution.Get("balance.P").Value).Should().BeLessThanOrEqualTo(1e-9);
        solution.PowerBalanceOk.Should().BeTrue();
    }

    [Fact]
    public void Two_sources_and_a_resistor_obey_kcl()
    {
        var circuit = new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 10))
            .Add(new DcVoltageSourceComponent("V2", 5))
            .Add(new ResistorComponent("R1", 100))
            .Add(new GroundComponent("GND"))
            .Connect("N1", ("V1", "+"), ("R1", "1"))
            .Connect("N2", ("V2", "+"), ("R1", "2"))
            .Connect("0", ("V1", "-"), ("V2", "-"), ("GND", "g"))
            .Build();

        var solution = _solver.Solve(circuit);
        solution.Get("R1.I").Value.Should().BeApproximately(0.05, 1e-9);
        solution.Get("V1.I").Value.Should().BeApproximately(-0.05, 1e-9);
        solution.Get("V2.I").Value.Should().BeApproximately(0.05, 1e-9);
        solution.Get("R1.P").Value.Should().BeApproximately(0.25, 1e-9);
        Math.Abs(solution.Get("balance.P").Value).Should().BeLessThanOrEqualTo(1e-9);
    }

    [Fact]
    public void Reverse_biased_led_is_open()
    {
        var circuit = new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 5))
            .Add(new ResistorComponent("R1", 1000))
            .Add(new LedComponent("L1", 3))
            .Add(new GroundComponent("GND"))
            .Connect("N1", ("V1", "+"), ("R1", "1"))
            .Connect("N2", ("R1", "2"), ("L1", "K"))
            .Connect("0", ("V1", "-"), ("L1", "A"), ("GND", "g"))
            .Build();

        var solution = _solver.Solve(circuit);
        solution.Get("L1.I").Value.Should().BeApproximately(0, 1e-12);
        solution.Get("L1.V").Value.Should().BeApproximately(-5, 1e-9);
        solution.Get("R1.I").Value.Should().BeApproximately(0, 1e-12);
    }

    [Fact]
    public void Capacitor_is_open_and_inductor_is_a_short()
    {
        var openCapacitor = new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 10))
            .Add(new CapacitorComponent("C1", 1e-6))
            .Add(new ResistorComponent("R1", 1000))
            .Add(new GroundComponent("GND"))
            .Connect("N1", ("V1", "+"), ("C1", "1"))
            .Connect("N2", ("C1", "2"), ("R1", "1"))
            .Connect("0", ("V1", "-"), ("R1", "2"), ("GND", "g"))
            .Build();
        var capacitor = _solver.Solve(openCapacitor);
        capacitor.Get("C1.I").Value.Should().BeApproximately(0, 1e-12);
        capacitor.Get("R1.I").Value.Should().BeApproximately(0, 1e-12);

        var shorted = new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 10))
            .Add(new InductorComponent("L1", 1e-3))
            .Add(new ResistorComponent("R1", 1000))
            .Add(new GroundComponent("GND"))
            .Connect("N1", ("V1", "+"), ("L1", "1"))
            .Connect("N2", ("L1", "2"), ("R1", "1"))
            .Connect("0", ("V1", "-"), ("R1", "2"), ("GND", "g"))
            .Build();
        var inductor = _solver.Solve(shorted);
        inductor.Get("L1.V").Value.Should().BeApproximately(0, 1e-9);
        inductor.Get("R1.I").Value.Should().BeApproximately(0.01, 1e-9);
        Math.Abs(inductor.Get("balance.P").Value).Should().BeLessThanOrEqualTo(1e-9);
    }

    [Fact]
    public void Shockley_diode_satisfies_kcl_and_the_diode_equation()
    {
        AssertDiode(DiodePresets.In4148);
        AssertDiode(DiodePresets.Schottky);
    }

    [Theory]
    [InlineData(1000, PowerRatingStatus.Ok, "OK")]
    [InlineData(800, PowerRatingStatus.Warning, "atenção")]
    [InlineData(400, PowerRatingStatus.Warning, "atenção")]
    [InlineData(200, PowerRatingStatus.Error, "excede a potência")]
    public void Power_rating_thresholds(double ohms, PowerRatingStatus status, string message)
    {
        var circuit = new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 10))
            .Add(new ResistorComponent("R1", ohms))
            .Add(new GroundComponent("GND"))
            .Connect("N1", ("V1", "+"), ("R1", "1"))
            .Connect("0", ("V1", "-"), ("R1", "2"), ("GND", "g"))
            .Build();

        var finding = _solver.Solve(circuit).PowerRatings.Should().ContainSingle().Subject;
        finding.ComponentId.Should().Be("R1");
        finding.Status.Should().Be(status, "ratio {0}, power {1}, rating {2}", finding.Ratio, finding.PowerWatts, finding.RatingWatts);
        finding.Message.Should().Be(message);
    }

    [Fact]
    public void Missing_ground_names_the_components()
    {
        var circuit = new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 5))
            .Add(new ResistorComponent("R1", 100))
            .Connect("A", ("V1", "+"), ("R1", "1"))
            .Connect("B", ("V1", "-"), ("R1", "2"))
            .Build();

        var act = () => _solver.Solve(circuit);
        var error = act.Should().Throw<MissingGroundException>().Which;
        error.Message.Should().Contain("terra").And.Contain("V1").And.Contain("R1");
        error.ComponentIds.Should().Contain("V1").And.Contain("R1");
    }

    [Fact]
    public void Floating_node_names_the_component()
    {
        var circuit = new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 5))
            .Add(new ResistorComponent("R1", 100))
            .Add(new ResistorComponent("R2", 100))
            .Add(new GroundComponent("GND"))
            .Connect("N1", ("V1", "+"), ("R1", "1"))
            .Connect("0", ("V1", "-"), ("R1", "2"), ("GND", "g"))
            .Connect("X", ("R2", "1"))
            .Connect("Y", ("R2", "2"))
            .Build();

        var act = () => _solver.Solve(circuit);
        var error = act.Should().Throw<FloatingNodeException>().Which;
        error.Message.Should().Contain("flutuante").And.Contain("R2");
        error.ComponentIds.Should().Contain("R2");
        error.NodeIds.Should().Contain("X").And.Contain("Y");
    }

    [Fact]
    public void Voltage_source_loop_names_both_sources()
    {
        var circuit = new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 5))
            .Add(new DcVoltageSourceComponent("V2", 3))
            .Add(new GroundComponent("GND"))
            .Connect("N", ("V1", "+"), ("V2", "+"))
            .Connect("0", ("V1", "-"), ("V2", "-"), ("GND", "g"))
            .Build();

        var act = () => _solver.Solve(circuit);
        var error = act.Should().Throw<VoltageSourceLoopException>().Which;
        error.Message.Should().Contain("tensão").And.Contain("V1").And.Contain("V2");
        error.ComponentIds.Should().Contain("V1").And.Contain("V2");
    }

    [Fact]
    public void Closed_switch_across_a_source_is_a_voltage_loop()
    {
        var circuit = new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 5))
            .Add(new SwitchComponent("S1", SwitchState.Closed))
            .Add(new GroundComponent("GND"))
            .Connect("N", ("V1", "+"), ("S1", "1"))
            .Connect("0", ("V1", "-"), ("S1", "2"), ("GND", "g"))
            .Build();

        var error = _solver.Invoking(solver => solver.Solve(circuit)).Should().Throw<VoltageSourceLoopException>().Which;
        error.ComponentIds.Should().Contain("V1").And.Contain("S1");
    }

    [Fact]
    public void Solve_reports_progress_and_honors_cancellation()
    {
        var circuit = Divider();
        var seen = new List<double>();
        var progress = new SyncProgress(seen.Add);
        var solution = _solver.Solve(circuit, progress: progress);
        seen.Should().NotBeEmpty();
        seen[0].Should().Be(0);
        seen[^1].Should().Be(1);
        seen.Should().OnlyContain(value => value >= 0 && value <= 1);
        solution.PowerBalanceOk.Should().BeTrue();

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var blocked = () => _solver.Solve(circuit, cancellationToken: cancelled.Token);
        blocked.Should().Throw<OperationCanceledException>();

        using var during = new CancellationTokenSource();
        var cancelling = new SyncProgress(_ => during.Cancel());
        var mid = () => _solver.Solve(circuit, progress: cancelling, cancellationToken: during.Token);
        mid.Should().Throw<OperationCanceledException>();
    }

    private void AssertDiode(DiodeModel model)
    {
        var circuit = new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 5))
            .Add(new ResistorComponent("R1", 1000))
            .Add(new DiodeComponent("D1", model))
            .Add(new GroundComponent("GND"))
            .Connect("N1", ("V1", "+"), ("R1", "1"))
            .Connect("N2", ("R1", "2"), ("D1", "A"))
            .Connect("0", ("V1", "-"), ("D1", "K"), ("GND", "g"))
            .Build();

        var solution = _solver.Solve(circuit);
        var current = solution.Get("D1.I").Value;
        var terminal = solution.Get("D1.V").Value;
        var junction = terminal - (current * model.SeriesResistance);
        var predicted = model.SaturationCurrent * (Math.Exp(junction / (model.EmissionCoefficient * DiodePhysics.ThermalVoltage)) - 1);
        Math.Abs(current - predicted).Should().BeLessThanOrEqualTo(1e-9);
        Math.Abs(solution.Get("R1.I").Value - current).Should().BeLessThanOrEqualTo(1e-9);
        Math.Abs(solution.Get("balance.P").Value).Should().BeLessThanOrEqualTo(1e-9);
        current.Should().BePositive();
    }

    private static CircuitModel Divider() => new CircuitBuilder()
        .Add(new DcVoltageSourceComponent("V1", 10))
        .Add(new ResistorComponent("R1", 1000))
        .Add(new ResistorComponent("R2", 1000))
        .Add(new GroundComponent("GND"))
        .Connect("N1", ("V1", "+"), ("R1", "1"))
        .Connect("N2", ("R1", "2"), ("R2", "1"))
        .Connect("0", ("V1", "-"), ("R2", "2"), ("GND", "g"))
        .Build();

    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
