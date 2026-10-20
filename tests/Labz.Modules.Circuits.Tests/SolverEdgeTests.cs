using System.Diagnostics;
using FluentAssertions;
using Labz.Modules.Circuits;

namespace Labz.Modules.Circuits.Tests;

public class SolverEdgeTests
{
    private readonly DcSolver _solver = new();

    [Fact]
    public void Reverse_diode_at_minus_100_volts_converges()
    {
        var circuit = new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", -100))
            .Add(new ResistorComponent("R1", 1000))
            .Add(new DiodeComponent("D1"))
            .Add(new GroundComponent("GND"))
            .Connect("N1", ("V1", "+"), ("R1", "1"))
            .Connect("N2", ("R1", "2"), ("D1", "A"))
            .Connect("0", ("V1", "-"), ("D1", "K"), ("GND", "g"))
            .Build();

        var solution = _solver.Solve(circuit);
        var model = DiodePresets.In4148;
        var current = solution.Get("D1.I").Value;
        var junction = solution.Get("D1.V").Value - (current * model.SeriesResistance);
        var predicted = model.SaturationCurrent * (Math.Exp(junction / (model.EmissionCoefficient * DiodePhysics.ThermalVoltage)) - 1);
        Math.Abs(current - predicted).Should().BeLessThanOrEqualTo(1e-9);
        Math.Abs(solution.Get("R1.I").Value - current).Should().BeLessThanOrEqualTo(1e-9);
        current.Should().BeNegative();
        solution.PowerBalanceOk.Should().BeTrue();
    }

    [Fact]
    public void Cancelling_a_long_ladder_stops_inside_elimination()
    {
        var circuit = Ladder(1800);
        using var cts = new CancellationTokenSource();
        Exception? error = null;
        var watch = Stopwatch.StartNew();
        var thread = new Thread(() =>
        {
            try
            {
                _solver.Solve(circuit, cancellationToken: cts.Token);
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.IsBackground = true;
        thread.Start();
        Thread.Sleep(100);
        cts.Cancel();
        var stopped = thread.Join(TimeSpan.FromMilliseconds(1500));
        stopped.Should().BeTrue("the token is checked inside Gaussian elimination, so the solve must not run to completion");
        error.Should().BeOfType<OperationCanceledException>();
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Progress_is_reported_from_inside_the_solve()
    {
        var seen = new List<double>();
        var progress = new SyncProgress(seen.Add);
        _solver.Solve(Divider(), progress: progress);

        seen[0].Should().Be(0);
        seen[^1].Should().Be(1);
        seen.Should().OnlyContain(value => value >= 0 && value <= 1);
        seen.Should().Contain(value => value > 0.2 && value < 0.95);
        seen.Count.Should().BeGreaterThan(3);
    }

    [Fact]
    public void A_net_named_like_a_component_is_a_typed_portuguese_error()
    {
        var act = () => new CircuitBuilder()
            .Add(new ResistorComponent("R1", 100))
            .Add(new GroundComponent("GND"))
            .Connect("R1", ("R1", "1"))
            .Connect("0", ("R1", "2"), ("GND", "g"))
            .Build();

        var error = act.Should().Throw<CircuitNameCollisionException>().Which;
        error.Message.Should().Contain("R1").And.Contain("componente").And.Contain("rede");
        error.ComponentIds.Should().Contain("R1");
    }

    [Fact]
    public void Invalid_component_values_use_portuguese_resource_text()
    {
        var resistance = () => new ResistorComponent("R1", double.NaN);
        resistance.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*finito*");

        var pin = () => new CircuitBuilder()
            .Add(new ResistorComponent("R1", 10))
            .Connect("N", ("R1", "9"))
            .Build();
        pin.Should().Throw<ArgumentException>().WithMessage("*pino*");
    }

    [Fact]
    public void Led_directly_across_a_source_reports_overcurrent()
    {
        var forward = new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 5))
            .Add(new LedComponent("L1", 3))
            .Add(new GroundComponent("GND"))
            .Connect("N", ("V1", "+"), ("L1", "A"))
            .Connect("0", ("V1", "-"), ("L1", "K"), ("GND", "g"))
            .Build();

        var error = _solver.Invoking(solver => solver.Solve(forward)).Should().Throw<OvercurrentException>().Which;
        error.Message.Should().Contain("Sobrecorrente").And.Contain("L1");
        error.Message.Should().NotContain("não convergiu");

        var reverse = new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 5))
            .Add(new LedComponent("L1", 3))
            .Add(new GroundComponent("GND"))
            .Connect("N", ("V1", "+"), ("L1", "K"))
            .Connect("0", ("V1", "-"), ("L1", "A"), ("GND", "g"))
            .Build();
        var open = _solver.Solve(reverse);
        open.Get("L1.I").Value.Should().BeApproximately(0, 1e-9);
    }

    [Theory]
    [InlineData(2, -0.1)]
    [InlineData(2, -1)]
    [InlineData(2, -5)]
    [InlineData(2, -100)]
    [InlineData(3, -0.1)]
    [InlineData(3, -1)]
    [InlineData(3, -5)]
    [InlineData(3, -100)]
    public void Series_diodes_reverse_biased_through_1k_converge(int count, double volts)
    {
        var solution = _solver.Solve(SeriesDiodes(count, volts, 1000));
        solution.PowerBalanceOk.Should().BeTrue();
        var resistorCurrent = solution.Get("R1.I").Value;
        for (var index = 1; index <= count; index++)
        {
            solution.Get($"D{index}.I").Value.Should().BeApproximately(resistorCurrent, 1e-9);
        }
    }

    [Theory]
    [InlineData(0.3)]
    [InlineData(0.7)]
    public void Three_forward_diodes_at_low_voltage_converge(double volts)
    {
        var solution = _solver.Solve(SeriesDiodes(3, volts, 1000));
        solution.PowerBalanceOk.Should().BeTrue();
        solution.Get("D1.I").Value.Should().BeApproximately(solution.Get("R1.I").Value, 1e-9);
    }

    [Fact]
    public void Diode_fed_by_1_gigaohm_at_5_volts_converges()
    {
        var solution = _solver.Solve(SeriesDiodes(1, 5, 1e9));
        solution.PowerBalanceOk.Should().BeTrue();
        solution.Get("D1.I").Value.Should().BePositive();
        solution.Get("D1.I").Value.Should().BeApproximately(solution.Get("R1.I").Value, 1e-9);
    }

    [Fact]
    public void Low_current_diode_residual_tracks_the_operating_current()
    {
        // Is = 10 fA through 1 GΩ at 0.35 V. A 1 nA Newton band is about 1e5 times Is and
        // leaves the diode current and the resistor current apart by tens of percent.
        var model = new DiodeModel(1e-14, 1, 0);
        var circuit = new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 0.35))
            .Add(new ResistorComponent("R1", 1e9))
            .Add(new DiodeComponent("D1", model))
            .Add(new GroundComponent("GND"))
            .Connect("N1", ("V1", "+"), ("R1", "1"))
            .Connect("N2", ("R1", "2"), ("D1", "A"))
            .Connect("0", ("V1", "-"), ("D1", "K"), ("GND", "g"))
            .Build();

        var solution = _solver.Solve(circuit);
        var current = solution.Get("D1.I").Value;
        var resistor = solution.Get("R1.I").Value;
        current.Should().BeInRange(1e-12, 1e-8);
        var scale = Math.Max(Math.Abs(current), model.SaturationCurrent);
        Math.Abs(current - resistor).Should().BeLessThanOrEqualTo(1e-4 * scale);
        solution.PowerBalanceOk.Should().BeTrue();
    }

    [Fact]
    public void Steep_diode_matches_the_full_model_current()
    {
        // Saturation of 1e-20 A clamps near 2 mA. The old slope test accepts that
        // point while the model current and the resistor current still disagree.
        var model = new DiodeModel(1e-20, 1, 0);
        var circuit = new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 2))
            .Add(new ResistorComponent("R1", 1))
            .Add(new DiodeComponent("D1", model))
            .Add(new GroundComponent("GND"))
            .Connect("N1", ("V1", "+"), ("R1", "1"))
            .Connect("N2", ("R1", "2"), ("D1", "A"))
            .Connect("0", ("V1", "-"), ("D1", "K"), ("GND", "g"))
            .Build();

        var solution = _solver.Solve(circuit);
        var current = solution.Get("D1.I").Value;
        var resistor = solution.Get("R1.I").Value;
        var scale = Math.Max(Math.Abs(current), model.SaturationCurrent);
        Math.Abs(current - resistor).Should().BeLessThanOrEqualTo(1e-6 * scale);
        current.Should().BePositive();
        solution.PowerBalanceOk.Should().BeTrue();
    }

    [Fact]
    public void Unbalanced_diode_solution_is_rejected()
    {
        // A divider balances. The watt is injected through the test hook, not by a bad circuit.
        var solver = new DcSolver { PowerBalanceInjectionWatts = 1 };
        var act = () => solver.Solve(Divider());
        act.Should().Throw<DcSolverConvergenceException>().WithMessage("*balanço de potência*");
    }

    [Fact]
    public void In4148_at_50_volts_through_1_ohm_matches_the_resistor_current()
    {
        var solution = _solver.Solve(SeriesDiodes(1, 50, 1));
        solution.PowerBalanceOk.Should().BeTrue();
        CurrentsMatch(solution, "D1");
    }

    [Fact]
    public void Microamp_diode_at_50_volts_through_1_ohm_matches_the_resistor_current()
    {
        var circuit = SeriesDiode(new DiodeModel(1e-6, 1, 0), 50, 1);
        var solution = _solver.Solve(circuit);
        solution.PowerBalanceOk.Should().BeTrue();
        CurrentsMatch(solution, "D1");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Series_diodes_at_minus_10_kilovolts_share_the_resistor_current(int count)
    {
        var solution = _solver.Solve(SeriesDiodes(count, -10_000, 1000));
        solution.PowerBalanceOk.Should().BeTrue();
        var resistor = solution.Get("R1.I").Value;
        for (var index = 1; index <= count; index++)
        {
            solution.Get($"D{index}.I").Value.Should().BeApproximately(resistor, 1e-9);
        }
    }

    [Fact]
    public void With_expression_rejects_a_negative_saturation_current()
    {
        var saturation = () => DiodePresets.In4148 with { SaturationCurrent = -1 };
        saturation.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*corrente de saturação (IS)*");

        var emission = () => DiodePresets.In4148 with { EmissionCoefficient = 0 };
        emission.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*coeficiente de emissão (N)*");

        var series = () => DiodePresets.In4148 with { SeriesResistance = double.NaN };
        series.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*resistência série (RS)*");
    }

    [Theory]
    [InlineData(0d, 1d, 0d)]
    [InlineData(-1e-9, 1d, 0d)]
    [InlineData(double.NaN, 1d, 0d)]
    [InlineData(double.PositiveInfinity, 1d, 0d)]
    [InlineData(1e-9, 0d, 0d)]
    [InlineData(1e-9, -1d, 0d)]
    [InlineData(1e-9, double.NaN, 0d)]
    [InlineData(1e-9, 1d, -1d)]
    [InlineData(1e-9, 1d, double.NaN)]
    [InlineData(1e-9, 1d, double.NegativeInfinity)]
    public void Invalid_diode_parameters_are_rejected(double saturationCurrent, double emissionCoefficient, double seriesResistance)
    {
        var act = () => new DiodeModel(saturationCurrent, emissionCoefficient, seriesResistance);
        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*finito*");
    }

    [Fact]
    public void Stiff_forward_diode_converges_with_junction_limiting()
    {
        var solution = _solver.Solve(SeriesDiodes(1, 10, 1));
        solution.PowerBalanceOk.Should().BeTrue();
        solution.Get("D1.I").Value.Should().BeGreaterThan(1);
    }

    [Fact]
    public void Led_reverse_nanoamp_turns_off()
    {
        // 1.99 V through 1 MΩ against a 2 V LED is about -10 nA while the LED is forced on.
        // That is below 1e-6 and above 1e-9, so the state tolerance must stay at 1e-9.
        var circuit = new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 1.99))
            .Add(new ResistorComponent("R1", 1_000_000))
            .Add(new LedComponent("L1", 2))
            .Add(new GroundComponent("GND"))
            .Connect("N", ("V1", "+"), ("R1", "1"))
            .Connect("A", ("R1", "2"), ("L1", "A"))
            .Connect("0", ("V1", "-"), ("L1", "K"), ("GND", "g"))
            .Build();

        var solution = _solver.Solve(circuit);
        solution.Get("L1.I").Value.Should().Be(0);
        solution.PowerBalanceOk.Should().BeTrue();
    }

    [Fact]
    public void Cir7_exactly_half_and_full_rating_are_warnings()
    {
        // CIR-7: under 50% is OK, 50–100% is "atenção", over 100% is an error.
        // §9.6 is the numeric tolerance. §9.2 names 50% as the warning threshold and 100% as the error threshold.
        Rating(800).Should().Be("atenção");
        Rating(400).Should().Be("atenção");
        Rating(200).Should().Be("excede a potência");
    }

    private string Rating(double ohms)
    {
        var circuit = new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 10))
            .Add(new ResistorComponent("R1", ohms))
            .Add(new GroundComponent("GND"))
            .Connect("N", ("V1", "+"), ("R1", "1"))
            .Connect("0", ("V1", "-"), ("R1", "2"), ("GND", "g"))
            .Build();
        return _solver.Solve(circuit).PowerRatings.Single().Message;
    }

    private static void CurrentsMatch(DcSolution solution, string diodeId)
    {
        var resistor = solution.Get("R1.I").Value;
        var diode = solution.Get(diodeId + ".I").Value;
        Math.Abs(diode - resistor).Should().BeLessThanOrEqualTo(Math.Max(1e-9, 1e-9 * Math.Abs(resistor)));
    }

    private static CircuitModel SeriesDiode(DiodeModel model, double volts, double ohms) => new CircuitBuilder()
        .Add(new DcVoltageSourceComponent("V1", volts))
        .Add(new ResistorComponent("R1", ohms))
        .Add(new DiodeComponent("D1", model))
        .Add(new GroundComponent("GND"))
        .Connect("N1", ("V1", "+"), ("R1", "1"))
        .Connect("N2", ("R1", "2"), ("D1", "A"))
        .Connect("0", ("V1", "-"), ("D1", "K"), ("GND", "g"))
        .Build();

    private static CircuitModel SeriesDiodes(int count, double volts, double ohms)
    {
        var builder = new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", volts))
            .Add(new ResistorComponent("R1", ohms))
            .Add(new GroundComponent("GND"));
        for (var index = 1; index <= count; index++)
        {
            builder.Add(new DiodeComponent($"D{index}"));
        }

        builder.Connect("SRC", ("V1", "+"), ("R1", "1"));
        builder.Connect("N1", ("R1", "2"), ("D1", "A"));
        for (var index = 1; index < count; index++)
        {
            builder.Connect($"M{index}", ($"D{index}", "K"), ($"D{index + 1}", "A"));
        }

        builder.Connect("0", ("V1", "-"), ($"D{count}", "K"), ("GND", "g"));
        return builder.Build();
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

    private static CircuitModel Ladder(int sections)
    {
        var builder = new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 10))
            .Add(new GroundComponent("GND"))
            .Connect("N0", ("V1", "+"))
            .Connect("0", ("V1", "-"), ("GND", "g"));
        for (var section = 0; section < sections; section++)
        {
            builder.Add(new ResistorComponent($"S{section}", 1000));
            builder.Add(new ResistorComponent($"P{section}", 1000));
            builder.Connect($"N{section}", ($"S{section}", "1"));
            builder.Connect($"N{section + 1}", ($"S{section}", "2"), ($"P{section}", "1"));
            builder.Connect("0", ($"P{section}", "2"));
        }

        return builder.Build();
    }

    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
