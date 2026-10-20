using System.Reflection;
using Labz.Core.Project;

namespace Labz.Modules.Circuits;

public static class Ref1Sample
{
    public const string ResourceName = "Labz.Modules.Circuits.REF-1.project.labz.json";

    public static ProjectDocument Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded sample '{ResourceName}' is missing.");
        using var reader = new StreamReader(stream);
        var result = ProjectLoader.Load(reader.ReadToEnd());
        if (result.Document is null)
        {
            throw new InvalidOperationException(result.Error);
        }

        return result.Document;
    }
}
