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

    public static string LibraryHeader => Get(nameof(LibraryHeader));

    public static string PropertiesHeader => Get(nameof(PropertiesHeader));

    public static string Undo => Get(nameof(Undo));

    public static string Redo => Get(nameof(Redo));

    public static string Rotate => Get(nameof(Rotate));

    public static string Delete => Get(nameof(Delete));

    public static string PlaceHint => Get(nameof(PlaceHint));

    public static string WireHint => Get(nameof(WireHint));

    public static string NoSelection => Get(nameof(NoSelection));

    public static string SwitchClosed => Get(nameof(SwitchClosed));

    public static string SwitchOpen => Get(nameof(SwitchOpen));

    public static string SchottkyPreset => Get(nameof(SchottkyPreset));

    public static string StandardDiodePreset => Get(nameof(StandardDiodePreset));

    public static string NewProjectName => Get(nameof(NewProjectName));

    public static string ColumnId => Get(nameof(ColumnId));

    public static string ColumnType => Get(nameof(ColumnType));

    public static string ColumnVoltage => Get(nameof(ColumnVoltage));

    public static string ColumnCurrent => Get(nameof(ColumnCurrent));

    public static string ColumnPower => Get(nameof(ColumnPower));

    public static string ColumnRating => Get(nameof(ColumnRating));

    public static string ValidationHeader => Get(nameof(ValidationHeader));

    public static string Calculating => Get(nameof(Calculating));

    public static string Cancel => Get(nameof(Cancel));

    public static string LoadHours => Get(nameof(LoadHours));

    public static string PeakSunHours => Get(nameof(PeakSunHours));

    public static string HoursInvalid => Get(nameof(HoursInvalid));

    public static string ChargeEfficiencyLabel => Get(nameof(ChargeEfficiencyLabel));

    public static string FixSchematicMessage => Get(nameof(FixSchematicMessage));

    private static string Get(string name)
    {
        return ResourceManager.GetString(name, CultureInfo.CurrentUICulture)
            ?? throw new InvalidOperationException($"Missing UI string '{name}'.");
    }
}
