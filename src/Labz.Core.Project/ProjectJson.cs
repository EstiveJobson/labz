using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Labz.Core.Project;

public static class ProjectJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static string Serialize(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(document, Options);
    }

    public static ProjectDocument Deserialize(string json)
    {
        return JsonSerializer.Deserialize<ProjectDocument>(json, Options)
            ?? throw new JsonException(Strings.InvalidFile());
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };
        options.Converters.Add(new PinEndpointConverter());
        return options;
    }
}

internal sealed class PinEndpointConverter : JsonConverter<PinEndpoint>
{
    public override PinEndpoint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException(Strings.InvalidFile());
        }

        reader.Read();
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException(Strings.InvalidFile());
        }

        var componentId = reader.GetString() ?? string.Empty;
        reader.Read();
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException(Strings.InvalidFile());
        }

        var pin = reader.GetString() ?? string.Empty;
        reader.Read();
        if (reader.TokenType != JsonTokenType.EndArray)
        {
            throw new JsonException(Strings.InvalidFile());
        }

        return new PinEndpoint { ComponentId = componentId, Pin = pin };
    }

    public override void Write(Utf8JsonWriter writer, PinEndpoint value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteStringValue(value.ComponentId);
        writer.WriteStringValue(value.Pin);
        writer.WriteEndArray();
    }
}
