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
    public void Row_scaling_keeps_a_tiny_pivot_row_accurate()
    {
        // Without row scaling the 1e16 row wins the pivot, the first unknown comes back as 2,
        // and refinement stops because the residual is small next to the largest right-hand side.
        var matrix = new double[,]
        {
            { 1e-16, 1, 0 },
            { 1, 1, 1 },
            { 0, 1, 1e16 },
        };
        var solution = DenseLinearSolver.Solve(matrix, [1, 3, 1e16]);
        solution[0].Should().BeApproximately(1, 1e-8);
        solution[1].Should().BeApproximately(1, 1e-8);
        solution[2].Should().BeApproximately(1, 1e-8);
    }

    [Fact]
    public void Ill_conditioned_solve_refines_without_a_second_factorization()
    {
        // A = Q diag(1e7, 1e3, 10, 1) Q^T with a Householder Q. The right-hand side is the
        // smallest singular direction, so the first residual sits above 1e-12 * ||rhs|| and
        // the correction reuses the same factors.
        var reflector = new double[] { 1, 2, 3, 4 };
        var matrix = new double[4, 4];
        var expected = new double[4];
        var rhs = new double[4];
        var spectrum = new double[] { 1e7, 1e3, 10, 1 };
        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < 4; column++)
            {
                var orthogonal = (row == column ? 1 : 0) - (reflector[row] * reflector[column] / 15d);
                expected[row] += orthogonal * (column == 3 ? 1 : 0);
                for (var mode = 0; mode < 4; mode++)
                {
                    var left = (row == mode ? 1 : 0) - (reflector[row] * reflector[mode] / 15d);
                    var right = (column == mode ? 1 : 0) - (reflector[mode] * reflector[column] / 15d);
                    matrix[row, column] += left * spectrum[mode] * right;
                }
            }
        }

        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < 4; column++)
            {
                rhs[row] += matrix[row, column] * expected[column];
            }
        }

        var factorsBefore = DenseLinearSolver.FactorizationCount;
        var refinementsBefore = DenseLinearSolver.RefinementCount;
        var solution = DenseLinearSolver.Solve(matrix, rhs);
        (DenseLinearSolver.FactorizationCount - factorsBefore).Should().Be(1);
        (DenseLinearSolver.RefinementCount - refinementsBefore).Should().BeGreaterThan(0);
        for (var index = 0; index < expected.Length; index++)
        {
            solution[index].Should().BeApproximately(expected[index], 1e-6);
        }
    }

    private static double Relative(double actual, double expected) =>
        Math.Abs(actual - expected) / Math.Abs(expected);
}
