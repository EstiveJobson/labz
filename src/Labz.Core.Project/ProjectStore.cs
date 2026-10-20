namespace Labz.Core.Project;

/// <summary>
/// A project folder: <c>project.labz.json</c>, a <c>results/</c> cache, and <c>history/</c> snapshots.
/// Results are never written into the project file.
/// </summary>
public static class ProjectStore
{
    public const string FileName = "project.labz.json";

    public const string ResultsDirectoryName = "results";

    public const string HistoryDirectoryName = "history";

    public static string ProjectPath(string folder) => Path.Combine(folder, FileName);

    public static void Save(string folder, ProjectDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(document);
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(Path.Combine(folder, ResultsDirectoryName));
        Directory.CreateDirectory(Path.Combine(folder, HistoryDirectoryName));
        File.WriteAllText(ProjectPath(folder), ProjectJson.Serialize(document));
    }

    public static ProjectLoadResult Open(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        var path = ProjectPath(folder);
        if (!File.Exists(path))
        {
            return ProjectLoadResult.Invalid(Strings.MissingFile());
        }

        try
        {
            return ProjectLoader.Load(File.ReadAllText(path));
        }
        catch (IOException)
        {
            return ProjectLoadResult.Invalid(Strings.Unreadable());
        }
        catch (UnauthorizedAccessException)
        {
            return ProjectLoadResult.Invalid(Strings.Unreadable());
        }
    }
}
