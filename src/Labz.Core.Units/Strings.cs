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

    public static string NonFinite(string text) =>
        Format("NonFinite", text);

    public static string AmbiguousSeparator(string text) =>
        Format("AmbiguousSeparator", text);

    public static string Dimensionless() => Format("Dimensionless");

    public static string NotANumber() => Format("NotANumber");

    public static string Infinity() => Format("Infinity");

    private static string Format(string name, params object[] args)
    {
        var template = ResourceManager.GetString(name, CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException($"Missing string '{name}'.");
        return string.Format(CultureInfo.InvariantCulture, template, args);
    }
}
