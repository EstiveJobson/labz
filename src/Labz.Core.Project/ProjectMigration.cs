using System.Text.Json.Nodes;

namespace Labz.Core.Project;

public interface IProjectMigration
{
    string FromVersion { get; }

    string ToVersion { get; }

    JsonObject Apply(JsonObject document);
}

public enum ProjectMigrationStatus
{
    Current,
    Migrated,
    Unknown,
}

public sealed class ProjectMigrationResult
{
    private ProjectMigrationResult(ProjectMigrationStatus status, JsonObject? document)
    {
        Status = status;
        Document = document;
    }

    public ProjectMigrationStatus Status { get; }

    public JsonObject? Document { get; }

    public static ProjectMigrationResult Current(JsonObject document) => new(ProjectMigrationStatus.Current, document);

    public static ProjectMigrationResult Migrated(JsonObject document) => new(ProjectMigrationStatus.Migrated, document);

    public static ProjectMigrationResult Unknown() => new(ProjectMigrationStatus.Unknown, null);
}

/// <summary>Walks version to version until the document is at <see cref="ProjectVersions.Current"/>.</summary>
public static class ProjectMigrationPipeline
{
    private static readonly IProjectMigration[] Steps = [new LegacyZeroToCurrentMigration()];

    private static readonly System.Text.RegularExpressions.Regex VersionToken = new(
        "^[0-9]+(\\.[0-9]+)?$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.Compiled);

    public static IReadOnlyList<IProjectMigration> Migrations => Steps;

    public static bool IsVersionToken(string version) => VersionToken.IsMatch(version);

    public static ProjectMigrationResult Migrate(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!TryReadVersion(document, out var version))
        {
            return ProjectMigrationResult.Unknown();
        }

        if (version == ProjectVersions.Current)
        {
            return ProjectMigrationResult.Current(document);
        }

        var migrated = false;
        while (version != ProjectVersions.Current)
        {
            var step = Steps.FirstOrDefault(candidate => candidate.FromVersion == version);
            if (step is null)
            {
                return ProjectMigrationResult.Unknown();
            }

            document = step.Apply(document);
            version = step.ToVersion;
            migrated = true;
        }

        return migrated
            ? ProjectMigrationResult.Migrated(document)
            : ProjectMigrationResult.Current(document);
    }

    internal static bool TryReadVersion(JsonObject document, out string version)
    {
        version = string.Empty;
        if (document["schemaVersion"] is not JsonValue value || !value.TryGetValue<string>(out var text))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        version = text;
        return true;
    }
}

/// <summary>
/// Test migration from the fake 0.0 shape (a root <c>circuit</c> object) to §6.1 <c>modules.circuits</c>.
/// </summary>
public sealed class LegacyZeroToCurrentMigration : IProjectMigration
{
    public string FromVersion => ProjectVersions.Legacy;

    public string ToVersion => ProjectVersions.Current;

    public JsonObject Apply(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var circuit = document["circuit"] as JsonObject;
        document.Remove("circuit");
        circuit ??= new JsonObject();
        EnsureArray(circuit, "components");
        EnsureArray(circuit, "nets");
        EnsureArray(circuit, "usageProfiles");
        EnsureArray(circuit, "analyses");
        document["modules"] = new JsonObject
        {
            ["circuits"] = circuit,
        };
        if (document["history"] is null)
        {
            document["history"] = new JsonArray();
        }

        if (document["idea"] is null)
        {
            document["idea"] = string.Empty;
        }

        document["schemaVersion"] = ToVersion;
        return document;
    }

    private static void EnsureArray(JsonObject owner, string name)
    {
        if (owner[name] is null)
        {
            owner[name] = new JsonArray();
        }
    }
}
