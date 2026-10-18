using System.Globalization;
using System.Resources;

namespace Labz.Core.Units;

internal static class Strings
{
    private static readonly ResourceManager ResourceManager = new(
        "Labz.Core.Units.Strings",
        typeof(Strings).Assembly);

    public static string DimensionMismatch(string left, string right) =>
        Format("DimensionMismatch", left, right);

    public static string UnitMismatch(string unitToken, string expected) =>
        Format("UnitMismatch", unitToken, expected);

    public static string FormatInvalid(string text) =>
        Format("FormatInvalid", text);

    private static string Format(string name, params object[] args)
    {
        var template = ResourceManager.GetString(name, CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException($"Missing string '{name}'.");
        return string.Format(CultureInfo.InvariantCulture, template, args);
    }
}
