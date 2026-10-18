using FluentAssertions;
using Labz.Core.Engine;
using Labz.Core.Units;

namespace Labz.Core.Engine.Tests;

public class EngineTests
{
    private readonly FormulaRegistry _formulas = StandardFormulas.Create();

    [Fact]
    public void Core6_result_records_id_value_unit_formula_and_inputs()
    {
        var store = new ResultStore();
        var result = new EngineResult("R1.P", 0.059208, "W", "ohm.P=I2R", ["R1.I", "R1.resistance"]);
        store.Add(result);

        var found = store.Get("R1.P");
        found.ResultId.Should().Be("R1.P");
        found.Value.Should().Be(0.059208);
        found.Unit.Should().Be("W");
        found.FormulaId.Should().Be("ohm.P=I2R");
        found.InputIds.Should().Equal("R1.I", "R1.resistance");
        store.TryGet("missing", out _).Should().BeFalse();
        var duplicate = () => store.Add(result);
        duplicate.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Formulas_declare_dimensions_and_match_the_ref1_hand_calc()
    {
        var ohm = _formulas.Get("ohm.I=V/R");
        ohm.Inputs.Select(input => input.Dimension).Should().Equal(Dimension.Voltage, Dimension.Resistance);
        ohm.Output.Dimension.Should().Be(Dimension.Current);

        var current = _formulas.EvaluateWorking("ohm.I=V/R", Quantity.Of(3, KnownUnits.Volt), Quantity.Of(15.1, KnownUnits.Ohm));
        Relative(current, 3.0 / 15.1).Should().BeLessThanOrEqualTo(1e-12);

        var power = _formulas.EvaluateWorking(
            "ohm.P=I2R",
            Quantity.Of(3.0 / 15.1 / 10, KnownUnits.Ampere),
            Quantity.Of(150, KnownUnits.Ohm));
        Relative(power, Math.Pow(3.0 / 15.1 / 10, 2) * 150).Should().BeLessThanOrEqualTo(1e-12);

        var wh = _formulas.EvaluateWorking(
            "energy.Wh_day",
            Quantity.Of(12 * (3.0 / 15.1), KnownUnits.Watt),
            Quantity.Of(6, KnownUnits.Hour));
        Relative(wh, 12 * (3.0 / 15.1) * 6).Should().BeLessThanOrEqualTo(1e-12);

        var runtime = _formulas.EvaluateWorking(
            "energy.runtime_h",
            Quantity.Of(7, KnownUnits.AmpereHour),
            Quantity.Of(0.5, KnownUnits.Dimensionless),
            Quantity.Of(3.0 / 15.1, KnownUnits.Ampere));
        Relative(runtime, 3.5 / (3.0 / 15.1)).Should().BeLessThanOrEqualTo(1e-12);

        _formulas.Get("energy.Wh_day").Output.Dimension.Should().Be(Dimension.EnergyPerDay);
        _formulas.Get("energy.Wh_day").Output.Dimension.Should().NotBe(Dimension.Energy);
        _formulas.Get("energy.Wh_day").Output.Unit.Symbol.Should().Be("Wh/day");

        var autonomy = _formulas.EvaluateWorking(
            "energy.autonomy_d",
            Quantity.Of(42, KnownUnits.WattHour),
            Quantity.Of(wh, KnownUnits.WattHourPerDay));
        Relative(autonomy, 42 / wh).Should().BeLessThanOrEqualTo(1e-12);

        var recharge = _formulas.EvaluateWorking(
            "energy.recharge_h",
            Quantity.Of(7, KnownUnits.AmpereHour),
            Quantity.Of(0.5, KnownUnits.Dimensionless),
            Quantity.Of(6.0 / 5.1, KnownUnits.Ampere),
            Quantity.Of(1, KnownUnits.Dimensionless));
        Relative(recharge, 3.5 / (6.0 / 5.1)).Should().BeLessThanOrEqualTo(1e-12);
    }

    [Fact]
    public void Formula_rejects_a_mismatched_input_dimension()
    {
        var act = () => _formulas.Evaluate("ohm.P=VI", Quantity.Of(1, KnownUnits.Volt), Quantity.Of(1, KnownUnits.Ohm));
        act.Should().Throw<DimensionMismatchException>();
    }

    [Fact]
    public void Dense_solver_handles_a_known_system_and_partial_pivoting()
    {
        var identity = DenseLinearSolver.Solve(new double[,] { { 2, 0 }, { 0, 4 } }, [6, 8]);
        identity[0].Should().BeApproximately(3, 1e-12);
        identity[1].Should().BeApproximately(2, 1e-12);

        // Rows must be swapped; the first pivot is zero.
        var pivoted = DenseLinearSolver.Solve(new double[,] { { 0, 1 }, { 1, 0 } }, [3, 4]);
        pivoted[0].Should().BeApproximately(4, 1e-12);
        pivoted[1].Should().BeApproximately(3, 1e-12);

        var singular = () => DenseLinearSolver.Solve(new double[,] { { 1, 2 }, { 2, 4 } }, [1, 2]);
        singular.Should().Throw<SingularLinearSystemException>();
    }

    [Fact]
    public void Solve_factors_the_matrix_once()
    {
        var before = DenseLinearSolver.FactorizationCount;
        DenseLinearSolver.Solve(new double[,] { { 3, 1 }, { 1, 2 } }, [5, 5]);
        (DenseLinearSolver.FactorizationCount - before).Should().Be(1);
    }

    private static double Relative(double actual, double expected) =>
        Math.Abs(actual - expected) / Math.Abs(expected);
}
