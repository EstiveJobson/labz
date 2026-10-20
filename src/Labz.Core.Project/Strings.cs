using System.Globalization;
using System.Resources;

namespace Labz.Core.Project;

internal static class Strings
{
    private static readonly ResourceManager ResourceManager = new(
        "Labz.Core.Project.Strings",
        typeof(Strings).Assembly);

    public static string NewerVersion() => Get("NewerVersion");

    public static string InvalidFile() => Get("InvalidFile");

    public static string Unreadable() => Get("Unreadable");

    public static string MissingFile() => Get("MissingFile");

    public static string DuplicateComponent(string id) => Format("DuplicateComponent", id);

    public static string UnknownComponent(string netId, string componentId) =>
        Format("UnknownComponent", netId, componentId);

    public static string UnknownPin(string componentId, string pin) =>
        Format("UnknownPin", componentId, pin);

    public static string PinConnectedTwice(string componentId, string pin) =>
        Format("PinConnectedTwice", componentId, pin);

    public static string ConnectionShape(string netId) => Format("ConnectionShape", netId);

    public static string AutosaveRange() => Get("AutosaveRange");

    public static string DigitsRange() => Get("DigitsRange");

    private static string Get(string name)
    {
        return ResourceManager.GetString(name, CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException($"Missing string '{name}'.");
    }

    private static string Format(string name, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, Get(name), args);
}
