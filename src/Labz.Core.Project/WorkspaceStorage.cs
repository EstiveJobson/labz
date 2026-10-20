using System.Text.Json;
using System.Text.Json.Nodes;

namespace Labz.Core.Project;

public sealed record RecentProject(string Folder, string Name, DateTimeOffset OpenedAt);

public static class RecentProjectStore
{
    public const int Capacity = 10;

    public static IReadOnlyList<RecentProject> Load(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<RecentProject>>(File.ReadAllText(path), ProjectJson.Options) ?? [];
            return items
                .Where(item => !string.IsNullOrWhiteSpace(item.Folder))
                .Take(Capacity)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static IReadOnlyList<RecentProject> Remember(string path, string folder, string name, DateTimeOffset openedAt)
    {
        var items = Load(path)
            .Where(item => !string.Equals(item.Folder, folder, StringComparison.OrdinalIgnoreCase))
            .Prepend(new RecentProject(folder, name, openedAt))
            .Take(Capacity)
            .ToArray();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(items, ProjectJson.Options));
        return items;
    }
}

public sealed class UserSettings
{
    public const int DefaultAutosaveMinutes = 2;

    public const int MinimumAutosaveMinutes = 1;

    public const int MaximumAutosaveMinutes = 60;

    public const int DefaultSignificantDigits = 6;

    public const int MinimumSignificantDigits = 1;

    public const int MaximumSignificantDigits = 8;

    public int AutosaveIntervalMinutes { get; set; } = DefaultAutosaveMinutes;

    public int SignificantDigits { get; set; } = DefaultSignificantDigits;

    public static bool TryAutosaveMinutes(int minutes, out string? error)
    {
        if (minutes is < MinimumAutosaveMinutes or > MaximumAutosaveMinutes)
        {
            error = Strings.AutosaveRange();
            return false;
        }

        error = null;
        return true;
    }

    public static bool TrySignificantDigits(int digits, out string? error)
    {
        if (digits is < MinimumSignificantDigits or > MaximumSignificantDigits)
        {
            error = Strings.DigitsRange();
            return false;
        }

        error = null;
        return true;
    }
}

public static class UserSettingsStore
{
    public static UserSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            return new UserSettings();
        }

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
            var settings = new UserSettings();
            if (root?["autosaveIntervalMinutes"] is JsonValue minutesValue
                && minutesValue.TryGetValue<int>(out var minutes)
                && UserSettings.TryAutosaveMinutes(minutes, out _))
            {
                settings.AutosaveIntervalMinutes = minutes;
            }

            if (root?["significantDigits"] is JsonValue digitsValue
                && digitsValue.TryGetValue<int>(out var digits)
                && UserSettings.TrySignificantDigits(digits, out _))
            {
                settings.SignificantDigits = digits;
            }

            return settings;
        }
        catch (JsonException)
        {
            return new UserSettings();
        }
    }

    public static void Save(string path, UserSettings settings)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(settings, ProjectJson.Options));
    }
}

public sealed class AutosavePolicy
{
    private readonly TimeProvider _clock;
    private readonly Func<int> _intervalMinutes;
    private DateTimeOffset _last;

    public AutosavePolicy(TimeProvider clock, Func<int> intervalMinutes)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _intervalMinutes = intervalMinutes ?? throw new ArgumentNullException(nameof(intervalMinutes));
        _last = clock.GetUtcNow();
    }

    public bool IsDue()
    {
        var minutes = _intervalMinutes();
        if (!UserSettings.TryAutosaveMinutes(minutes, out _))
        {
            minutes = UserSettings.DefaultAutosaveMinutes;
        }

        return _clock.GetUtcNow() - _last >= TimeSpan.FromMinutes(minutes);
    }

    public void MarkSaved() => _last = _clock.GetUtcNow();
}

public static class SnapshotStore
{
    public const int Capacity = 20;

    public static void Write(string projectFolder, ProjectDocument document, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFolder);
        ArgumentNullException.ThrowIfNull(document);
        var history = Path.Combine(projectFolder, ProjectStore.HistoryDirectoryName);
        Directory.CreateDirectory(history);
        var stamp = at.ToUniversalTime().ToString("yyyyMMddHHmmssfffffff", System.Globalization.CultureInfo.InvariantCulture);
        var path = Path.Combine(history, stamp + ".json");
        var suffix = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(history, stamp + "-" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".json");
            suffix++;
        }

        File.WriteAllText(path, ProjectJson.Serialize(document));
        foreach (var extra in List(projectFolder).Take(Math.Max(0, List(projectFolder).Count - Capacity)))
        {
            File.Delete(extra);
        }
    }

    public static IReadOnlyList<string> List(string projectFolder)
    {
        var history = Path.Combine(projectFolder, ProjectStore.HistoryDirectoryName);
        if (!Directory.Exists(history))
        {
            return [];
        }

        return Directory.GetFiles(history, "*.json")
            .Select(path => (Path: path, Name: ParseName(path)))
            .OrderBy(item => item.Name.Stamp, StringComparer.Ordinal)
            .ThenBy(item => item.Name.Suffix)
            .Select(item => item.Path)
            .ToArray();
    }

    private static (string Stamp, int Suffix) ParseName(string path)
    {
        var file = Path.GetFileNameWithoutExtension(path);
        var dash = file.LastIndexOf('-');
        if (dash > 0 && int.TryParse(file[(dash + 1)..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var suffix))
        {
            return (file[..dash], suffix);
        }

        return (file, 0);
    }
}
