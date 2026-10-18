using System.Globalization;
using FluentAssertions;
using Labz.Core.Units;
using Labz.Modules.Circuits;
using Xunit.Abstractions;

namespace Labz.Modules.Circuits.Tests;

/// <summary>
/// REF-1 is built as ten branches. The 15 Ω figure is only the hand-calc equivalent from §9.
/// Printed spec numbers are rounded; the 1e-6 check is against the formula on the same line.
/// </summary>
public class Ref1Tests
{
    private readonly ITestOutputHelper _output;
    private readonly DcSolver _solver = new();

    public Ref1Tests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Scenario_A_night_discharge()
    {
        var solution = Solve(SwitchState.Open, SwitchState.Closed);
        const double emf = 12;
        const double rint = 0.1;
        const double branch = 150;
        const double equivalent = branch / 10;
        var current = (emf - 9) / (equivalent + rint);
        var terminal = emf - (rint * current);
        var branchCurrent = current / 10;

        // §9.2 Total current: I = (12.0 − 9.0) / (15 + 0.1) = 3 / 15.1 = 0.198675 A
        AssertSpec(-solution.Get("BAT1.I").Value, current, "0.198675", "§9.2 total current");
        Math.Abs(solution.Get("S2.I").Value - (-solution.Get("BAT1.I").Value)).Should().BeLessThanOrEqualTo(1e-9);

        // §9.2 Battery terminal voltage: Vt = 12.0 − 0.1 × 0.198675 = 11.980132 V
        AssertSpec(solution.Get("BAT1.V").Value, terminal, "11.980132", "§9.2 Vt");
        AssertSpec(solution.Get("N_BAT.V").Value, terminal, "11.980132", "§9.2 N_BAT");

        for (var branchIndex = 1; branchIndex <= 10; branchIndex++)
        {
            // §9.2 Per-branch current: Ib = 0.0198675 A (19.87 mA)
            AssertSpec(solution.Get($"R{branchIndex}.I").Value, branchCurrent, "0.0198675", $"§9.2 Ib R{branchIndex}");
            // §9.2 Resistor power: P_R = Ib² × 150 = 0.059208 W
            AssertSpec(solution.Get($"R{branchIndex}.P").Value, branchCurrent * branchCurrent * branch, "0.059208", $"§9.2 P_R R{branchIndex}");
            var ledPower = solution.Get($"L{branchIndex}A.P").Value
                + solution.Get($"L{branchIndex}B.P").Value
                + solution.Get($"L{branchIndex}C.P").Value;
            // §9.2 LED power per branch: 3 × 3.0 × Ib = 0.178808 W
            AssertSpec(ledPower, 9 * branchCurrent, "0.178808", $"§9.2 P_LED branch {branchIndex}");
        }

        QuantityFormatter.Format(new Quantity(solution.Get("R1.I").Value, Dimension.Current)).Should().Be("19,87 mA");

        // §9.2 Load power: P_load = Vt × I = 2.380159 W
        AssertSpec(solution.Get("load.P").Value, terminal * current, "2.380159", "§9.2 P_load");
        // §9.2 Battery internal loss: I² × 0.1 = 0.003947 W
        AssertSpec(solution.Get("BAT1.Rint.P").Value, current * current * rint, "0.003947", "§9.2 Rint");
        // §9.2 Power drawn from the battery EMF: 12.0 × I = 2.384106 W
        AssertSpec(solution.Get("energy.P_source").Value, emf * current, "2.384106", "§9.2 P_emf");
        // §9.2 Energy per day: 2.384106 W × 6 h = 14.3046 Wh/day
        AssertSpec(solution.Get("energy.Wh_day").Value, emf * current * 6, "14.3046", "§9.2 Wh/day");
        // §9.2 Usable capacity: 7.0 Ah × 0.5 = 3.5 Ah, or 42.0 Wh at 12 V
        AssertSpec(solution.Get("energy.usable.Ah").Value, 3.5, "3.5", "§9.2 usable Ah");
        AssertSpec(solution.Get("energy.usable.Wh").Value, 42, "42.0", "§9.2 usable Wh");
        // §9.2 Runtime (continuous): 3.5 Ah / 0.198675 A = 17.617 h
        AssertSpec(solution.Get("energy.runtime.h").Value, 3.5 / current, "17.617", "§9.2 runtime");
        // §9.2 Autonomy: 42.0 / 14.3046 = 2.936 days
        AssertSpec(solution.Get("energy.autonomy.d").Value, 42 / (emf * current * 6), "2.936", "§9.2 autonomy");

        solution.PowerRatings.Should().OnlyContain(rating => rating.Status == PowerRatingStatus.Ok && rating.Message == "OK");
        AssertBalance(solution);
    }

    [Fact]
    public void Scenario_B_day_charge_only()
    {
        var solution = Solve(SwitchState.Closed, SwitchState.Open);
        var charge = 6.0 / 5.1;
        var terminal = 12 + (0.1 * charge);

        // §9.3 Charge current: Ich = (18.0 − 12.0) / (5.0 + 0.1) = 6 / 5.1 = 1.176471 A
        AssertSpec(solution.Get("BAT1.I").Value, charge, "1.176471", "§9.3 Ich");
        // §9.3 Battery terminal voltage: Vt = 12.0 + 0.1 × 1.176471 = 12.117647 V
        AssertSpec(solution.Get("BAT1.V").Value, terminal, "12.117647", "§9.3 Vt");
        // §9.3 Panel output power at the terminals: Vt × Ich = 14.256055 W
        AssertSpec(-solution.Get("PV1.P").Value, terminal * charge, "14.256055", "§9.3 P_panel");
        // §9.3 Power stored (into the EMF): 12.0 × Ich = 14.117647 W
        AssertSpec(solution.Get("energy.P_stored").Value, 12 * charge, "14.117647", "§9.3 P_stored");
        // §9.3 Panel internal (Rs) loss: Ich² × 5 = 6.920415 W
        AssertSpec(solution.Get("PV1.Rs.P").Value, charge * charge * 5, "6.920415", "§9.3 Rs");
        // §9.3 Source total: 18 × Ich = 21.176471 W
        AssertSpec(-solution.Get("PV1.emf.P").Value, 18 * charge, "21.176471", "§9.3 P_source");
        // §9.3 Charge energy per day: 14.117647 W × 5 h = 70.588 Wh/day
        AssertSpec(solution.Get("energy.charge.Wh_day").Value, 12 * charge * 5, "70.588", "§9.3 Wh/day");
        // §9.3 Time to recharge the usable 3.5 Ah: 3.5 / 1.176471 = 2.975 h
        AssertSpec(solution.Get("energy.recharge.h").Value, 3.5 / charge, "2.975", "§9.3 recharge");
        AssertBalance(solution);
    }

    [Fact]
    public void Scenario_C_day_with_load()
    {
        var solution = Solve(SwitchState.Closed, SwitchState.Closed);
        var node = 1863.0 / 154.0;
        var panel = (18 - node) / 5;
        var load = (node - 9) / 15;
        var battery = (node - 12) / 0.1;
        var branchCurrent = load / 10;

        // §9.4 Node voltage: V = 1863/154 = 12.097403 V
        AssertSpec(solution.Get("N_BAT.V").Value, node, "12.097403", "§9.4 V");
        // §9.4 Panel current: (18 − V)/5 = 1.180519 A
        AssertSpec(-solution.Get("PV1.I").Value, panel, "1.180519", "§9.4 I_panel");
        // §9.4 Load current: (V − 9)/15 = 0.206494 A
        AssertSpec(solution.Get("S2.I").Value, load, "0.206494", "§9.4 I_load");
        // §9.4 Battery charge current: (V − 12)/0.1 = 0.974026 A
        AssertSpec(solution.Get("BAT1.I").Value, battery, "0.974026", "§9.4 I_bat");
        Math.Abs((-solution.Get("PV1.I").Value) - (solution.Get("S2.I").Value + solution.Get("BAT1.I").Value))
            .Should().BeLessThanOrEqualTo(1e-9);

        for (var branchIndex = 1; branchIndex <= 10; branchIndex++)
        {
            // §9.4 per branch, from 0.0206494² × 150
            AssertSpec(solution.Get($"R{branchIndex}.I").Value, branchCurrent, "0.0206494", $"§9.4 Ib R{branchIndex}");
            // §9.4 Resistor power per branch: 0.0206494² × 150 = 0.063959 W
            AssertSpec(solution.Get($"R{branchIndex}.P").Value, branchCurrent * branchCurrent * 150, "0.063959", $"§9.4 P_R R{branchIndex}");
        }

        QuantityFormatter.Format(new Quantity(solution.Get("R1.I").Value, Dimension.Current)).Should().Be("20,65 mA");
        solution.PowerRatings.Should().OnlyContain(rating => rating.Message == "OK");

        // §9.4 panel EMF: 18 × 1.180519 = 21.249351 W
        AssertSpec(-solution.Get("PV1.emf.P").Value, 18 * panel, "21.249351", "§9.4 P_emf");
        // §9.4 Rs loss: 6.968131 W
        AssertSpec(solution.Get("PV1.Rs.P").Value, panel * panel * 5, "6.968131", "§9.4 Rs");
        // §9.4 load: V × 0.206494 = 2.498035 W
        AssertSpec(solution.Get("load.P").Value, node * load, "2.498035", "§9.4 P_load");
        // §9.4 stored: 12 × 0.974026 = 11.688312 W
        AssertSpec(solution.Get("energy.P_stored").Value, 12 * battery, "11.688312", "§9.4 P_stored");
        // §9.4 Rint loss: 0.094873 W
        AssertSpec(solution.Get("BAT1.Rint.P").Value, battery * battery * 0.1, "0.094873", "§9.4 Rint");
        AssertBalance(solution);
    }

    [Fact]
    public void Cir7_fifteen_ohm_branches_exceed_the_power_rating()
    {
        var solution = Solve(SwitchState.Open, SwitchState.Closed, branchOhms: 15);
        solution.PowerRatings.Should().OnlyContain(rating =>
            rating.Status == PowerRatingStatus.Error && rating.Message == "excede a potência");
        foreach (var rating in solution.PowerRatings)
        {
            var current = solution.Get(rating.ComponentId + ".I").Value;
            _output.WriteLine($"CIR-7 15 Ω {rating.ComponentId}.I = {current:G17} A");
        }

        AssertBalance(solution);
    }

    private DcSolution Solve(SwitchState s1, SwitchState s2, double branchOhms = 150) =>
        _solver.Solve(Ref1Circuit.Build(s1, s2, branchOhms), new EnergySettings(6, 5));

    private void AssertBalance(DcSolution solution)
    {
        var balance = solution.Get("balance.P").Value;
        _output.WriteLine($"balance.P = {balance:G17}");
        Math.Abs(balance).Should().BeLessThanOrEqualTo(1e-9);
        solution.PowerBalanceOk.Should().BeTrue();
    }

    private void AssertSpec(double actual, double handCalc, string printed, string line)
    {
        var relative = Math.Abs(actual - handCalc) / Math.Abs(handCalc);
        var decimals = printed.Contains('.') ? printed.Length - printed.IndexOf('.') - 1 : 0;
        var rounded = Math.Round(actual, decimals, MidpointRounding.AwayFromZero)
            .ToString("F" + decimals, CultureInfo.InvariantCulture);
        _output.WriteLine($"{line} | spec {printed} | engine {actual:G17} | hand {handCalc:G17} | rel {relative:E3} | rounded {rounded}");
        relative.Should().BeLessThanOrEqualTo(1e-6, line);
        rounded.Should().Be(printed, line);
    }
}

internal static class Ref1Circuit
{
    public static CircuitModel Build(SwitchState s1, SwitchState s2, double branchOhms)
    {
        var builder = new CircuitBuilder()
            .Add(new SolarPanelComponent("PV1", 18, 5, ratedPowerWatts: 20))
            .Add(new BatteryComponent("BAT1", 12, 0.1, 7, 0.5))
            .Add(new SwitchComponent("S1", s1))
            .Add(new SwitchComponent("S2", s2))
            .Add(new GroundComponent("GND"));

        for (var branch = 1; branch <= 10; branch++)
        {
            builder
                .Add(new ResistorComponent($"R{branch}", branchOhms, 0.25))
                .Add(new LedComponent($"L{branch}A", 3, forwardCurrent: 0.02, color: "branco"))
                .Add(new LedComponent($"L{branch}B", 3, forwardCurrent: 0.02, color: "branco"))
                .Add(new LedComponent($"L{branch}C", 3, forwardCurrent: 0.02, color: "branco"));
        }

        builder
            .Connect("PV_OUT", ("PV1", "+"), ("S1", "1"))
            .Connect("N_BAT", ("S1", "2"), ("BAT1", "+"), ("S2", "1"))
            .Connect("N_LOAD", ("S2", "2"))
            .Connect("0", ("PV1", "-"), ("BAT1", "-"), ("GND", "g"));

        for (var branch = 1; branch <= 10; branch++)
        {
            builder
                .Connect("N_LOAD", ($"R{branch}", "1"))
                .Connect($"NR{branch}", ($"R{branch}", "2"), ($"L{branch}A", "A"))
                .Connect($"N{branch}AB", ($"L{branch}A", "K"), ($"L{branch}B", "A"))
                .Connect($"N{branch}BC", ($"L{branch}B", "K"), ($"L{branch}C", "A"))
                .Connect("0", ($"L{branch}C", "K"));
        }

        return builder.Build();
    }
}
