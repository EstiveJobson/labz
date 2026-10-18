namespace Labz.Core.Units;

/// <summary>
/// A named unit. <see cref="SiPerUnit"/> converts a number in this unit into the SI quantity stored on <see cref="Quantity"/>.
/// </summary>
public sealed record Unit(string Symbol, Dimension Dimension, double SiPerUnit)
{
    public double ToSi(double valueInUnit) => valueInUnit * SiPerUnit;

    public double FromSi(double siValue) => siValue / SiPerUnit;
}
