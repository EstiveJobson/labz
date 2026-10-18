using Labz.Core.Units;

namespace Labz.Core.Engine;

/// <summary>Named pure formulas. The engine is the only place a number is calculated.</summary>
public sealed class FormulaRegistry
{
    private readonly Dictionary<string, Formula> _formulas = new(StringComparer.Ordinal);

    public IReadOnlyCollection<Formula> All => _formulas.Values.ToArray();

    public void Register(Formula formula)
    {
        ArgumentNullException.ThrowIfNull(formula);
        if (!_formulas.TryAdd(formula.Id, formula))
        {
            throw new InvalidOperationException($"Formula '{formula.Id}' is already registered.");
        }
    }

    public Formula Get(string id)
    {
        if (TryGet(id, out var formula))
        {
            return formula!;
        }

        throw new KeyNotFoundException($"No formula '{id}'.");
    }

    public bool TryGet(string id, out Formula? formula)
    {
        if (id is null)
        {
            formula = null;
            return false;
        }

        return _formulas.TryGetValue(id, out formula);
    }

    public Quantity Evaluate(string id, params Quantity[] arguments) => Get(id).Evaluate(arguments);

    public double EvaluateWorking(string id, params Quantity[] arguments) => Get(id).EvaluateWorking(arguments);
}
