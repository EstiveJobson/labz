using System.Text.Json;
using System.Text.Json.Nodes;

namespace Labz.Core.Project;

public enum ProjectLoadStatus
{
    Success,
    NewerVersion,
    Invalid,
}

public sealed class ProjectLoadResult
{
    private ProjectLoadResult(ProjectLoadStatus status, ProjectDocument? document, string? error, bool migrated)
    {
        Status = status;
        Document = document;
        Error = error;
        Migrated = migrated;
    }

    public ProjectLoadStatus Status { get; }

    public ProjectDocument? Document { get; }

    public string? Error { get; }

    public bool Migrated { get; }

    public static ProjectLoadResult Success(ProjectDocument document, bool migrated) =>
        new(ProjectLoadStatus.Success, document, null, migrated);

    public static ProjectLoadResult Newer(string error) =>
        new(ProjectLoadStatus.NewerVersion, null, error, false);

    public static ProjectLoadResult Invalid(string error) =>
        new(ProjectLoadStatus.Invalid, null, error, false);
}

public static class ProjectLoader
{
    public static ProjectLoadResult Load(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ProjectLoadResult.Invalid(Strings.InvalidFile());
        }

        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(json);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return ProjectLoadResult.Invalid(Strings.InvalidFile());
        }

        if (parsed is not JsonObject root)
        {
            return ProjectLoadResult.Invalid(Strings.InvalidFile());
        }

        ProjectMigrationResult migration;
        try
        {
            if (!ProjectMigrationPipeline.TryReadVersion(root, out _))
            {
                return ProjectLoadResult.Invalid(Strings.InvalidFile());
            }

            migration = ProjectMigrationPipeline.Migrate(root);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return ProjectLoadResult.Invalid(Strings.InvalidFile());
        }

        if (migration.Status == ProjectMigrationStatus.Unknown || migration.Document is null)
        {
            ProjectMigrationPipeline.TryReadVersion(root, out var version);
            return ProjectMigrationPipeline.IsVersionToken(version)
                ? ProjectLoadResult.Newer(Strings.NewerVersion())
                : ProjectLoadResult.Invalid(Strings.InvalidFile());
        }

        string canonical;
        try
        {
            canonical = migration.Document.ToJsonString();
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return ProjectLoadResult.Invalid(Strings.InvalidFile());
        }

        try
        {
            using var schemaDocument = JsonDocument.Parse(canonical);
            if (!ProjectSchemaValidator.IsValid(schemaDocument.RootElement))
            {
                return ProjectLoadResult.Invalid(Strings.InvalidFile());
            }

            var model = schemaDocument.Deserialize<ProjectDocument>(ProjectJson.Options);
            if (model is null)
            {
                return ProjectLoadResult.Invalid(Strings.InvalidFile());
            }

            var structural = ProjectStructure.Check(model);
            if (structural is not null)
            {
                return ProjectLoadResult.Invalid(structural);
            }

            return ProjectLoadResult.Success(model, migration.Status == ProjectMigrationStatus.Migrated);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return ProjectLoadResult.Invalid(Strings.InvalidFile());
        }
    }
}

internal static class ProjectStructure
{
    public static string? Check(ProjectDocument document)
    {
        var components = new Dictionary<string, ComponentDocument>(StringComparer.Ordinal);
        foreach (var component in document.Modules.Circuits.Components)
        {
            if (!components.TryAdd(component.Id, component))
            {
                return Strings.DuplicateComponent(component.Id);
            }
        }

        foreach (var component in document.Modules.Circuits.Components)
        {
            if (!double.IsFinite(component.Layout.X)
                || !double.IsFinite(component.Layout.Y)
                || !double.IsFinite(component.Layout.Rotation))
            {
                return Strings.NonFiniteNumber();
            }

            if (component.Layout.Rotation is not (0 or 90 or 180 or 270))
            {
                return Strings.RotationStep();
            }
        }

        foreach (var profile in document.Modules.Circuits.UsageProfiles)
        {
            if (!double.IsFinite(profile.HoursPerDay) || profile.HoursPerDay is < 0 or > 24)
            {
                return Strings.UsageHours();
            }
        }

        var seenPins = new HashSet<string>(StringComparer.Ordinal);
        foreach (var net in document.Modules.Circuits.Nets)
        {
            foreach (var connection in net.Connections)
            {
                if (string.IsNullOrWhiteSpace(connection.ComponentId) || string.IsNullOrWhiteSpace(connection.Pin))
                {
                    return Strings.ConnectionShape(net.Id);
                }

                if (!components.TryGetValue(connection.ComponentId, out var component))
                {
                    return Strings.UnknownComponent(net.Id, connection.ComponentId);
                }

                if (!component.Pins.Contains(connection.Pin, StringComparer.Ordinal))
                {
                    return Strings.UnknownPin(component.Id, connection.Pin);
                }

                if (!seenPins.Add(component.Id + "." + connection.Pin))
                {
                    return Strings.PinConnectedTwice(component.Id, connection.Pin);
                }
            }
        }

        return null;
    }
}
