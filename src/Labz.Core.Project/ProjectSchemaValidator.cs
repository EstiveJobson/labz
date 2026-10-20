using System.Text.Json;
using Json.Schema;

namespace Labz.Core.Project;

internal static class ProjectSchemaValidator
{
    private const string ResourceName = "Labz.Core.Project.project.v0.json";

    private static readonly JsonSchema Schema = Load();

    public static bool IsValid(JsonElement instance) => Schema.Evaluate(instance).IsValid;

    private static JsonSchema Load()
    {
        using var stream = typeof(ProjectSchemaValidator).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded schema '{ResourceName}' is missing.");
        using var reader = new StreamReader(stream);
        var options = new BuildOptions
        {
            Dialect = Dialect.Draft202012,
        };
        return JsonSchema.FromText(reader.ReadToEnd(), options);
    }
}
