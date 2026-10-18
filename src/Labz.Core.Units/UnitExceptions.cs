namespace Labz.Core.Units;

/// <summary>Adding or subtracting two quantities whose dimensions differ.</summary>
public sealed class DimensionMismatchException : Exception
{
    public DimensionMismatchException(Dimension left, Dimension right)
        : base(Strings.DimensionMismatch(KnownUnits.Describe(left), KnownUnits.Describe(right)))
    {
        Left = left;
        Right = right;
    }

    public Dimension Left { get; }

    public Dimension Right { get; }
}

/// <summary>A quantity string could not be parsed.</summary>
public class QuantityFormatException : Exception
{
    public QuantityFormatException(string message)
        : base(message)
    {
    }
}

/// <summary>The parsed unit does not have the dimension the caller asked for.</summary>
public sealed class UnitMismatchException : QuantityFormatException
{
    public UnitMismatchException(string unitToken, Dimension expected)
        : base(Strings.UnitMismatch(unitToken, KnownUnits.Describe(expected)))
    {
        UnitToken = unitToken;
        Expected = expected;
    }

    public string UnitToken { get; }

    public Dimension Expected { get; }
}
