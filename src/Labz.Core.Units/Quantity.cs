namespace Labz.Core.Units;

/// <summary>
/// A scalar stored in SI together with its dimension. Arithmetic refuses to add different dimensions.
/// </summary>
public readonly record struct Quantity
{
    public Quantity(double value, Dimension dimension)
    {
        Value = value;
        Dimension = dimension;
    }

    /// <summary>Value in SI units of <see cref="Dimension"/> (ohm, volt, joule, coulomb, second, …).</summary>
    public double Value { get; }

    public Dimension Dimension { get; }

    public static Quantity Of(double valueInUnit, Unit unit) => new(unit.ToSi(valueInUnit), unit.Dimension);

    public double In(Unit unit)
    {
        if (unit.Dimension != Dimension)
        {
            throw new DimensionMismatchException(Dimension, unit.Dimension);
        }

        return unit.FromSi(Value);
    }

    public static Quantity operator +(Quantity left, Quantity right)
    {
        EnsureSameDimension(left, right);
        return new Quantity(left.Value + right.Value, left.Dimension);
    }

    public static Quantity operator -(Quantity left, Quantity right)
    {
        EnsureSameDimension(left, right);
        return new Quantity(left.Value - right.Value, left.Dimension);
    }

    public static Quantity operator -(Quantity value) => new(-value.Value, value.Dimension);

    public static Quantity operator *(Quantity left, Quantity right) =>
        new(left.Value * right.Value, left.Dimension * right.Dimension);

    public static Quantity operator /(Quantity left, Quantity right)
    {
        if (right.Value == 0)
        {
            throw new DivideByZeroException(Strings.DivideByZero());
        }

        return new Quantity(left.Value / right.Value, left.Dimension / right.Dimension);
    }

    public static Quantity operator *(Quantity quantity, double scale) => new(quantity.Value * scale, quantity.Dimension);

    public static Quantity operator *(double scale, Quantity quantity) => quantity * scale;

    public static Quantity operator /(Quantity quantity, double scale)
    {
        if (scale == 0)
        {
            throw new DivideByZeroException(Strings.DivideByZero());
        }

        return new Quantity(quantity.Value / scale, quantity.Dimension);
    }

    public override string ToString() => QuantityFormatter.Format(this);

    private static void EnsureSameDimension(Quantity left, Quantity right)
    {
        if (left.Dimension != right.Dimension)
        {
            throw new DimensionMismatchException(left.Dimension, right.Dimension);
        }
    }
}
