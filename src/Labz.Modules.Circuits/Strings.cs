using System.Globalization;
using System.Resources;

namespace Labz.Modules.Circuits;

internal static class Strings
{
    private static readonly ResourceManager ResourceManager = new(
        "Labz.Modules.Circuits.Strings",
        typeof(Strings).Assembly);

    public static string MissingGround(string components) => Format("MissingGround", components);

    public static string FloatingNode(string nodes, string components) => Format("FloatingNode", nodes, components);

    public static string VoltageSourceLoop(string components) => Format("VoltageSourceLoop", components);

    public static string Singular(string components) => Format("Singular", components);

    public static string NoConvergence(string components) => Format("NoConvergence", components);

    public static string PowerBalance(string components) => Format("PowerBalance", components);

    public static string PowerRatingOk => Get("PowerRatingOk");

    public static string PowerRatingWarning => Get("PowerRatingWarning");

    public static string PowerRatingExceeded => Get("PowerRatingExceeded");

    public static string Overcurrent(string componentId, string others) =>
        Format("Overcurrent", componentId, others);

    public static string NameCollision(string name) => Format("NameCollision", name);

    public static string DuplicateComponent(string id) => Format("DuplicateComponent", id);

    public static string NetIdRequired() => Get("NetIdRequired");

    public static string ComponentIdRequired() => Get("ComponentIdRequired");

    public static string UnknownComponent(string netId, string componentId) =>
        Format("UnknownComponent", netId, componentId);

    public static string UnknownPin(string componentId, string pin) =>
        Format("UnknownPin", componentId, pin);

    public static string PinConnectedTwice(string componentId, string pin) =>
        Format("PinConnectedTwice", componentId, pin);

    public static string PositiveFinite(string name) => Format("PositiveFinite", name);

    public static string DiodeSaturationCurrent() => Get("DiodeSaturationCurrent");

    public static string DiodeEmissionCoefficient() => Get("DiodeEmissionCoefficient");

    public static string DiodeSeriesResistance() => Get("DiodeSeriesResistance");

    public static string NonNegativeFinite(string name) => Format("NonNegativeFinite", name);

    public static string FiniteValue(string name) => Format("FiniteValue", name);

    public static string DepthOfDischarge() => Get("DepthOfDischarge");

    public static string UnknownType(string type) => Format("UnknownType", type);

    public static string IdPattern(string id) => Format("IdPattern", id);

    public static string IdExhausted(string prefix) => Format("IdExhausted", prefix);

    public static string UnknownParam(string name) => Format("UnknownParam", name);

    public static string RequiredParam(string label) => Format("RequiredParam", label);

    public static string StateValue() => Get("StateValue");

    public static string UnconnectedPin(string componentId, string pin) =>
        Format("UnconnectedPin", componentId, pin);

    public static string VoltageShort(string components) => Format("VoltageShort", components);

    public static string TypeResistor => Get("TypeResistor");

    public static string TypeCapacitor => Get("TypeCapacitor");

    public static string TypeInductor => Get("TypeInductor");

    public static string TypeDiode => Get("TypeDiode");

    public static string TypeLed => Get("TypeLed");

    public static string TypeVoltageSource => Get("TypeVoltageSource");

    public static string TypeSolarPanel => Get("TypeSolarPanel");

    public static string TypeBattery => Get("TypeBattery");

    public static string TypeSwitch => Get("TypeSwitch");

    public static string TypeGround => Get("TypeGround");

    public static string ParamResistance => Get("ParamResistance");

    public static string ParamPowerRating => Get("ParamPowerRating");

    public static string ParamCapacitance => Get("ParamCapacitance");

    public static string ParamInitialVoltage => Get("ParamInitialVoltage");

    public static string ParamInductance => Get("ParamInductance");

    public static string ParamInitialCurrent => Get("ParamInitialCurrent");

    public static string ParamSaturationCurrent => Get("ParamSaturationCurrent");

    public static string ParamSeriesResistance => Get("ParamSeriesResistance");

    public static string ParamEmission => Get("ParamEmission");

    public static string ParamForwardVoltage => Get("ParamForwardVoltage");

    public static string ParamForwardCurrent => Get("ParamForwardCurrent");

    public static string ParamColor => Get("ParamColor");

    public static string ParamVoltage => Get("ParamVoltage");

    public static string ParamOpenCircuitVoltage => Get("ParamOpenCircuitVoltage");

    public static string ParamRatedPower => Get("ParamRatedPower");

    public static string ParamNominalVoltage => Get("ParamNominalVoltage");

    public static string ParamInternalResistance => Get("ParamInternalResistance");

    public static string ParamCapacity => Get("ParamCapacity");

    public static string ParamDepthOfDischarge => Get("ParamDepthOfDischarge");

    public static string ParamState => Get("ParamState");

    public static string ParamRon => Get("ParamRon");

    public static string ParamRoff => Get("ParamRoff");

    public static string FixSchematic => Get("FixSchematic");

    public static string BalanceOk => Get("BalanceOk");

    public static string BalanceBad => Get("BalanceBad");

    public static string SignConvention => Get("SignConvention");

    public static string CardWhPerDay => Get("CardWhPerDay");

    public static string CardAutonomy => Get("CardAutonomy");

    public static string CardChargeCurrent => Get("CardChargeCurrent");

    public static string CardRecharge => Get("CardRecharge");

    public static string ChargeEfficiency => Get("ChargeEfficiency");

    public static string MissingValue => Get("MissingValue");

    public static string HoursInvalid() => Get("HoursInvalid");

    public static string CardConsumption => Get("CardConsumption");

    public static string CardChargeEnergy => Get("CardChargeEnergy");

    private static string Get(string name)
    {
        return ResourceManager.GetString(name, CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException($"Missing string '{name}'.");
    }

    private static string Format(string name, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, Get(name), args);
}
