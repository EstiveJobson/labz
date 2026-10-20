namespace Labz.Modules.Circuits;

/// <summary>
/// Shockley parameters used by the DC equation. Reverse breakdown is not part of this model.
/// Assignment, including a record <c>with</c> expression, rejects a non-finite or out-of-range value.
/// </summary>
public sealed record DiodeModel
{
    private readonly double _saturationCurrent;
    private readonly double _emissionCoefficient;
    private readonly double _seriesResistance;

    public DiodeModel(double saturationCurrent, double emissionCoefficient, double seriesResistance)
    {
        SaturationCurrent = saturationCurrent;
        EmissionCoefficient = emissionCoefficient;
        SeriesResistance = seriesResistance;
    }

    public double SaturationCurrent
    {
        get => _saturationCurrent;
        init => _saturationCurrent = ParameterValidation.RequirePositive(value, Strings.PositiveFinite(Strings.DiodeSaturationCurrent()));
    }

    public double EmissionCoefficient
    {
        get => _emissionCoefficient;
        init => _emissionCoefficient = ParameterValidation.RequirePositive(value, Strings.PositiveFinite(Strings.DiodeEmissionCoefficient()));
    }

    public double SeriesResistance
    {
        get => _seriesResistance;
        init => _seriesResistance = ParameterValidation.RequireNonNegative(value, Strings.NonNegativeFinite(Strings.DiodeSeriesResistance()));
    }

    public void EnsureValid()
    {
        ParameterValidation.RequirePositive(SaturationCurrent, Strings.PositiveFinite(Strings.DiodeSaturationCurrent()));
        ParameterValidation.RequirePositive(EmissionCoefficient, Strings.PositiveFinite(Strings.DiodeEmissionCoefficient()));
        ParameterValidation.RequireNonNegative(SeriesResistance, Strings.NonNegativeFinite(Strings.DiodeSeriesResistance()));
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
        saturationCurrent: 2.682e-9,
        emissionCoefficient: 1.836,
        seriesResistance: 0.5664);

    public static DiodeModel Schottky { get; } = new(
        saturationCurrent: 31.7e-6,
        emissionCoefficient: 1.373,
        seriesResistance: 0.051);
}
