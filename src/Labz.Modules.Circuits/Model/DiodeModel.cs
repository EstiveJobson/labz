namespace Labz.Modules.Circuits;

/// <summary>
/// Shockley diode parameters. Breakdown voltage is stored for the later SPICE model and is not part of the v0 DC equation.
/// </summary>
public sealed record DiodeModel(
    double SaturationCurrent,
    double EmissionCoefficient,
    double SeriesResistance,
    double BreakdownVoltage)
{
    public void EnsureValid()
    {
        if (!double.IsFinite(SaturationCurrent) || SaturationCurrent <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SaturationCurrent));
        }

        if (!double.IsFinite(EmissionCoefficient) || EmissionCoefficient <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(EmissionCoefficient));
        }

        if (!double.IsFinite(SeriesResistance) || SeriesResistance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SeriesResistance));
        }

        if (!double.IsFinite(BreakdownVoltage) || BreakdownVoltage <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BreakdownVoltage));
        }
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
        SeriesResistance: 0.5664,
        BreakdownVoltage: 100);

    public static DiodeModel Schottky { get; } = new(
        SaturationCurrent: 31.7e-6,
        EmissionCoefficient: 1.373,
        SeriesResistance: 0.051,
        BreakdownVoltage: 40);
}
