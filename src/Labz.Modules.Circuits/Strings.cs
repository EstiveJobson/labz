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

    private static string Get(string name)
    {
        return ResourceManager.GetString(name, CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException($"Missing string '{name}'.");
    }

    private static string Format(string name, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, Get(name), args);
}
