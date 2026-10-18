namespace Labz.Core.Units;

/// <summary>
/// Exponents of the seven SI base units. Addition is legal only when every exponent matches.
/// </summary>
public readonly record struct Dimension(int Meter, int Kilogram, int Second, int Ampere, int Kelvin, int Mole, int Candela)
{
    public static Dimension None { get; } = new(0, 0, 0, 0, 0, 0, 0);

    public static Dimension Time { get; } = new(0, 0, 1, 0, 0, 0, 0);

    public static Dimension Current { get; } = new(0, 0, 0, 1, 0, 0, 0);

    public static Dimension Frequency { get; } = new(0, 0, -1, 0, 0, 0, 0);

    /// <summary>Volt: kg·m²·s⁻³·A⁻¹.</summary>
    public static Dimension Voltage { get; } = new(2, 1, -3, -1, 0, 0, 0);

    /// <summary>Ohm: kg·m²·s⁻³·A⁻².</summary>
    public static Dimension Resistance { get; } = new(2, 1, -3, -2, 0, 0, 0);

    /// <summary>Watt: kg·m²·s⁻³.</summary>
    public static Dimension Power { get; } = new(2, 1, -3, 0, 0, 0, 0);

    /// <summary>Farad: kg⁻¹·m⁻²·s⁴·A².</summary>
    public static Dimension Capacitance { get; } = new(-2, -1, 4, 2, 0, 0, 0);

    /// <summary>Henry: kg·m²·s⁻²·A⁻².</summary>
    public static Dimension Inductance { get; } = new(2, 1, -2, -2, 0, 0, 0);

    /// <summary>Joule, and therefore watt-hour: kg·m²·s⁻².</summary>
    public static Dimension Energy { get; } = new(2, 1, -2, 0, 0, 0, 0);

    /// <summary>Coulomb, and therefore ampere-hour: A·s.</summary>
    public static Dimension Charge { get; } = new(0, 0, 1, 1, 0, 0, 0);

    public bool IsDimensionless =>
        Meter == 0
        && Kilogram == 0
        && Second == 0
        && Ampere == 0
        && Kelvin == 0
        && Mole == 0
        && Candela == 0;

    public static Dimension operator *(Dimension left, Dimension right) => new(
        left.Meter + right.Meter,
        left.Kilogram + right.Kilogram,
        left.Second + right.Second,
        left.Ampere + right.Ampere,
        left.Kelvin + right.Kelvin,
        left.Mole + right.Mole,
        left.Candela + right.Candela);

    public static Dimension operator /(Dimension left, Dimension right) => new(
        left.Meter - right.Meter,
        left.Kilogram - right.Kilogram,
        left.Second - right.Second,
        left.Ampere - right.Ampere,
        left.Kelvin - right.Kelvin,
        left.Mole - right.Mole,
        left.Candela - right.Candela);
}
