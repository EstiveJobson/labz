using System.Globalization;
using System.Resources;

namespace Labz.App.Resources;

public static class Strings
{
    private static readonly ResourceManager ResourceManager = new(
        "Labz.App.Resources.Strings",
        typeof(Strings).Assembly);

    public static string AppTitle => Get(nameof(AppTitle));

    public static string NavHome => Get(nameof(NavHome));

    public static string NavSchematic => Get(nameof(NavSchematic));

    public static string NavSimulation => Get(nameof(NavSimulation));

    public static string NavResults => Get(nameof(NavResults));

    public static string NavSettings => Get(nameof(NavSettings));

    public static string UnderConstruction => Get(nameof(UnderConstruction));

    private static string Get(string name)
    {
        return ResourceManager.GetString(name, CultureInfo.CurrentUICulture)
            ?? throw new InvalidOperationException($"Missing UI string '{name}'.");
    }
}
