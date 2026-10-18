using Labz.Core.Units;

namespace Labz.Core.Engine;

/// <summary>One input or the output of a registered formula, with the unit the pure function speaks.</summary>
public sealed record FormulaPort(string Name, Dimension Dimension, Unit Unit);

/// <summary>
/// A named pure function. Arguments and the returned <see cref="Quantity"/> are SI;
/// <see cref="EvaluateWorking"/> returns the number in <see cref="Output"/>'s unit.
/// </summary>
public sealed class Formula
{
    private readonly Func<IReadOnlyList<double>, double> _evaluate;

    public Formula(
        string id,
        IReadOnlyList<FormulaPort> inputs,
        FormulaPort output,
        Func<IReadOnlyList<double>, double> evaluate)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A formula id is required.", nameof(id));
        }

        Id = id;
        Inputs = inputs?.ToArray() ?? throw new ArgumentNullException(nameof(inputs));
        Output = output ?? throw new ArgumentNullException(nameof(output));
        _evaluate = evaluate ?? throw new ArgumentNullException(nameof(evaluate));
    }

    public string Id { get; }

    public IReadOnlyList<FormulaPort> Inputs { get; }

    public FormulaPort Output { get; }

    public Quantity Evaluate(params Quantity[] arguments) => Evaluate((IReadOnlyList<Quantity>)arguments);

    public Quantity Evaluate(IReadOnlyList<Quantity> arguments)
    {
        var working = ToWorkingUnits(arguments);
        var value = _evaluate(working);
        return new Quantity(Output.Unit.ToSi(value), Output.Dimension);
    }

    public double EvaluateWorking(params Quantity[] arguments) => EvaluateWorking((IReadOnlyList<Quantity>)arguments);

    public double EvaluateWorking(IReadOnlyList<Quantity> arguments) => _evaluate(ToWorkingUnits(arguments));

    private double[] ToWorkingUnits(IReadOnlyList<Quantity> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count != Inputs.Count)
        {
            throw new ArgumentException($"Formula '{Id}' expects {Inputs.Count} arguments.", nameof(arguments));
        }

        var working = new double[arguments.Count];
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].Dimension != Inputs[i].Dimension)
            {
                throw new DimensionMismatchException(arguments[i].Dimension, Inputs[i].Dimension);
            }

            working[i] = Inputs[i].Unit.FromSi(arguments[i].Value);
        }

        return working;
    }
}
