using System.Globalization;
using System.Text.RegularExpressions;
using Labz.Core.Drawing;
using Labz.Core.Units;

namespace Labz.Modules.Circuits;

public enum ParamKind
{
    Quantity,
    Text,
    State,
}

public sealed class ParamDefinition
{
    public required string Name { get; init; }

    public required string Label { get; init; }

    public required ParamKind Kind { get; init; }

    public Dimension Dimension { get; init; }

    public string? DefaultText { get; init; }

    public bool Optional { get; init; }
}

public sealed class ComponentDefinition
{
    public required string Type { get; init; }

    public required string DisplayName { get; init; }

    public required string IdPrefix { get; init; }

    public required IReadOnlyList<string> Pins { get; init; }

    public required IReadOnlyList<ParamDefinition> Params { get; init; }

    public required SymbolGeometry Symbol { get; init; }
}

public static class ComponentIds
{
    public static readonly Regex Pattern = new("^[A-Z]{1,4}[0-9]{1,3}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string Next(string prefix, IEnumerable<string> existing)
    {
        var used = existing.ToHashSet(StringComparer.Ordinal);
        for (var number = 1; number <= 999; number++)
        {
            var id = prefix + number.ToString(CultureInfo.InvariantCulture);
            if (used.Contains(id))
            {
                continue;
            }

            if (!Pattern.IsMatch(id))
            {
                throw new InvalidOperationException(Strings.IdPattern(id));
            }

            return id;
        }

        throw new InvalidOperationException(Strings.IdExhausted(prefix));
    }
}

public static class ComponentLibrary
{
    public const string LoadTarget = "LOAD";

    public const double ChargeEfficiency = 1;

    private static readonly ComponentDefinition[] Definitions = Build();

    public static IReadOnlyList<ComponentDefinition> All => Definitions;

    public static ComponentDefinition Get(string type)
    {
        var definition = Definitions.FirstOrDefault(candidate => candidate.Type == type);
        if (definition is null)
        {
            throw new ArgumentException(Strings.UnknownType(type), nameof(type));
        }

        return definition;
    }

    public static bool TryGet(string type, out ComponentDefinition? definition)
    {
        definition = Definitions.FirstOrDefault(candidate => candidate.Type == type);
        return definition is not null;
    }

    public static IReadOnlyDictionary<string, string> DiodePreset(bool schottky)
    {
        var model = schottky ? DiodePresets.Schottky : DiodePresets.In4148;
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["IS"] = Amperes(model.SaturationCurrent),
            ["N"] = Plain(model.EmissionCoefficient),
            ["RS"] = Ohms(model.SeriesResistance),
            ["BV"] = Volts(model.BreakdownVoltage),
        };
    }

    private static ComponentDefinition[] Build() =>
    [
        Part("resistor", Strings.TypeResistor, "R", ["1", "2"],
        [
            Quantity("resistance", Strings.ParamResistance, Dimension.Resistance, "1 kΩ"),
            Quantity("powerRating", Strings.ParamPowerRating, Dimension.Power, "0,25 W"),
        ]),
        Part("capacitor", Strings.TypeCapacitor, "C", ["1", "2"],
        [
            Quantity("capacitance", Strings.ParamCapacitance, Dimension.Capacitance, "100 nF"),
            Quantity("initialVoltage", Strings.ParamInitialVoltage, Dimension.Voltage, null, optional: true),
        ]),
        Part("inductor", Strings.TypeInductor, "L", ["1", "2"],
        [
            Quantity("inductance", Strings.ParamInductance, Dimension.Inductance, "1 mH"),
            Quantity("initialCurrent", Strings.ParamInitialCurrent, Dimension.Current, null, optional: true),
        ]),
        Part("diode", Strings.TypeDiode, "D", ["A", "K"], DiodeParams()),
        Part("led", Strings.TypeLed, "LED", ["A", "K"],
        [
            Quantity("forwardVoltage", Strings.ParamForwardVoltage, Dimension.Voltage, "3 V"),
            Quantity("forwardCurrent", Strings.ParamForwardCurrent, Dimension.Current, "20 mA", optional: true),
            Text("color", Strings.ParamColor, "branco"),
        ]),
        Part("dc_voltage_source", Strings.TypeVoltageSource, "V", ["+", "-"],
        [
            Quantity("voltage", Strings.ParamVoltage, Dimension.Voltage, "5 V"),
        ]),
        Part("solar_panel", Strings.TypeSolarPanel, "PV", ["+", "-"],
        [
            Quantity("openCircuitVoltage", Strings.ParamOpenCircuitVoltage, Dimension.Voltage, "18 V"),
            Quantity("seriesResistance", Strings.ParamSeriesResistance, Dimension.Resistance, "5 Ω"),
            Quantity("ratedPower", Strings.ParamRatedPower, Dimension.Power, "20 W", optional: true),
        ]),
        Part("battery", Strings.TypeBattery, "BAT", ["+", "-"],
        [
            Quantity("nominalVoltage", Strings.ParamNominalVoltage, Dimension.Voltage, "12 V"),
            Quantity("internalResistance", Strings.ParamInternalResistance, Dimension.Resistance, "0,1 Ω"),
            Quantity("capacity", Strings.ParamCapacity, Dimension.Charge, "7 Ah"),
            Quantity("usableDepthOfDischarge", Strings.ParamDepthOfDischarge, Dimension.None, "0,5"),
        ]),
        Part("switch", Strings.TypeSwitch, "S", ["1", "2"],
        [
            State("state", Strings.ParamState, "open"),
            Quantity("Ron", Strings.ParamRon, Dimension.Resistance, "1 mΩ"),
            Quantity("Roff", Strings.ParamRoff, Dimension.Resistance, "1 GΩ"),
        ]),
        Part("ground", Strings.TypeGround, "GND", ["g"], []),
    ];

    private static ParamDefinition[] DiodeParams()
    {
        var preset = DiodePreset(schottky: false);
        return
        [
            Quantity("IS", Strings.ParamSaturationCurrent, Dimension.Current, preset["IS"]),
            Quantity("N", Strings.ParamEmission, Dimension.None, preset["N"]),
            Quantity("RS", Strings.ParamSeriesResistance, Dimension.Resistance, preset["RS"]),
            Quantity("BV", Strings.ParamBreakdown, Dimension.Voltage, preset["BV"]),
        ];
    }

    private static ComponentDefinition Part(
        string type,
        string displayName,
        string prefix,
        string[] pins,
        ParamDefinition[] parameters) => new()
    {
        Type = type,
        DisplayName = displayName,
        IdPrefix = prefix,
        Pins = pins,
        Params = parameters,
        Symbol = SymbolCatalog.For(type),
    };

    private static ParamDefinition Quantity(string name, string label, Dimension dimension, string? defaultText, bool optional = false) =>
        new()
        {
            Name = name,
            Label = label,
            Kind = ParamKind.Quantity,
            Dimension = dimension,
            DefaultText = defaultText,
            Optional = optional,
        };

    private static ParamDefinition Text(string name, string label, string defaultText) => new()
    {
        Name = name,
        Label = label,
        Kind = ParamKind.Text,
        DefaultText = defaultText,
    };

    private static ParamDefinition State(string name, string label, string defaultText) => new()
    {
        Name = name,
        Label = label,
        Kind = ParamKind.State,
        DefaultText = defaultText,
    };

    private static string Amperes(double value) => Plain(value) + " A";

    private static string Ohms(double value) => Plain(value) + " Ω";

    private static string Volts(double value) => Plain(value) + " V";

    private static string Plain(double value) =>
        value.ToString("G17", CultureInfo.InvariantCulture).Replace('.', ',');
}
