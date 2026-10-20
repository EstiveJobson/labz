namespace Labz.Modules.Circuits;

/// <summary>
/// Shockley parameters used by the DC equation. Reverse breakdown is not part of this model.
/// </summary>
public sealed record DiodeModel(double SaturationCurrent, double EmissionCoefficient, double SeriesResistance)
{
    public double SaturationCurrent { get; init; } = Require(SaturationCurrent, EmissionCoefficient, SeriesResistance).Current;

    public double EmissionCoefficient { get; init; } = Require(SaturationCurrent, EmissionCoefficient, SeriesResistance).Emission;

    public double SeriesResistance { get; init; } = Require(SaturationCurrent, EmissionCoefficient, SeriesResistance).Resistance;

    public void EnsureValid() => Require(SaturationCurrent, EmissionCoefficient, SeriesResistance);

    private readonly record struct Checked(double Current, double Emission, double Resistance);

    private static Checked Require(double saturationCurrent, double emissionCoefficient, double seriesResistance)
    {
        if (!double.IsFinite(saturationCurrent) || saturationCurrent <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SaturationCurrent), Strings.PositiveFinite(nameof(SaturationCurrent)));
        }

        if (!double.IsFinite(emissionCoefficient) || emissionCoefficient <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(EmissionCoefficient), Strings.PositiveFinite(nameof(EmissionCoefficient)));
        }

        if (!double.IsFinite(seriesResistance) || seriesResistance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SeriesResistance), Strings.NonNegativeFinite(nameof(SeriesResistance)));
        }

        return new Checked(saturationCurrent, emissionCoefficient, seriesResistance);
    }
}

public static class DiodePhysics
{
    public const double Boltzmann = 1.380649e-23;

    public const double ElementaryCharge = 1.602176634e-19;

    public const double ReferenceTemperatureKelvin = 300;

    public static double ThermalVoltage { get; } = Boltzmann * ReferenceTemperatureKelvin / ElementaryCharge;
}

/// <summary>1N4148-like defaults and a 1N5819-like Schottky preset.</summary>
public static class DiodePresets
{
    public static DiodeModel In4148 { get; } = new(
        SaturationCurrent: 2.682e-9,
        EmissionCoefficient: 1.836,
        SeriesResistance: 0.5664);

    public static DiodeModel Schottky { get; } = new(
        SaturationCurrent: 31.7e-6,
        EmissionCoefficient: 1.373,
        SeriesResistance: 0.051);
}
