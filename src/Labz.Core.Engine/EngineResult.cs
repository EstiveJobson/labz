namespace Labz.Core.Engine;

/// <summary>
/// One number produced by the engine. The value is expressed in <see cref="Unit"/> and can be traced through <see cref="FormulaId"/>.
/// </summary>
public sealed record EngineResult
{
    public EngineResult(
        string resultId,
        double value,
        string unit,
        string formulaId,
        IReadOnlyList<string> inputIds)
    {
        if (string.IsNullOrWhiteSpace(resultId))
        {
            throw new ArgumentException("A result id is required.", nameof(resultId));
        }

        if (string.IsNullOrWhiteSpace(formulaId))
        {
            throw new ArgumentException("A formula id is required.", nameof(formulaId));
        }

        ResultId = resultId;
        Value = value;
        Unit = unit ?? throw new ArgumentNullException(nameof(unit));
        FormulaId = formulaId;
        InputIds = inputIds?.ToArray() ?? throw new ArgumentNullException(nameof(inputIds));
    }

    public string ResultId { get; }

    public double Value { get; }

    public string Unit { get; }

    public string FormulaId { get; }

    public IReadOnlyList<string> InputIds { get; }
}
