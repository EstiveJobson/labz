namespace Labz.Core.Project;

/// <summary>The current project file schema. Older files migrate to this version.</summary>
public static class ProjectVersions
{
    public const string Current = "0.1";

    /// <summary>Fake predecessor used to prove the migration pipeline. It is not a shipped format.</summary>
    public const string Legacy = "0.0";
}

/// <summary>
/// One LABZ project, shaped exactly like <c>project.labz.json</c> in SPEC §6.1.
/// Params stay strings with units. Results are never part of this document.
/// Peak-sun hours are a <see cref="UsageProfileDocument"/> whose target is the panel id.
/// Charge efficiency is not stored; the engine default is 1.0.
/// </summary>
public sealed class ProjectDocument
{
    public string SchemaVersion { get; set; } = ProjectVersions.Current;

    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public string Idea { get; set; } = string.Empty;

    public ProjectModules Modules { get; set; } = new();

    public List<ProjectHistoryEntry> History { get; set; } = [];
}

public sealed class ProjectModules
{
    public CircuitDocument Circuits { get; set; } = new();
}

public sealed class CircuitDocument
{
    public List<ComponentDocument> Components { get; set; } = [];

    public List<NetDocument> Nets { get; set; } = [];

    public List<UsageProfileDocument> UsageProfiles { get; set; } = [];

    public List<AnalysisDocument> Analyses { get; set; } = [];
}

public sealed class ComponentDocument
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public Dictionary<string, string> Params { get; set; } = new(StringComparer.Ordinal);

    public List<string> Pins { get; set; } = [];

    public LayoutDocument Layout { get; set; } = new();
}

public sealed class LayoutDocument
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Rotation { get; set; }
}

public sealed class NetDocument
{
    public string Id { get; set; } = string.Empty;

    public List<PinEndpoint> Connections { get; set; } = [];
}

/// <summary>One <c>[componentId, pin]</c> pair inside a net.</summary>
public sealed class PinEndpoint
{
    public string ComponentId { get; set; } = string.Empty;

    public string Pin { get; set; } = string.Empty;
}

public sealed class UsageProfileDocument
{
    public string Target { get; set; } = string.Empty;

    public double HoursPerDay { get; set; }
}

public sealed class AnalysisDocument
{
    public string Type { get; set; } = string.Empty;

    public string? Step { get; set; }

    public string? Stop { get; set; }
}

public sealed class ProjectHistoryEntry
{
    public DateTimeOffset At { get; set; }

    public string Source { get; set; } = "user";

    public string Change { get; set; } = string.Empty;
}
