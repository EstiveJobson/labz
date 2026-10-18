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

    public static string NonNegativeFinite(string name) => Format("NonNegativeFinite", name);

    public static string FiniteValue(string name) => Format("FiniteValue", name);

    public static string DepthOfDischarge() => Get("DepthOfDischarge");

    private static string Get(string name)
    {
        return ResourceManager.GetString(name, CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException($"Missing string '{name}'.");
    }

    private static string Format(string name, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, Get(name), args);
}
