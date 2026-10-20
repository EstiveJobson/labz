using System.Text;

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
        var target = ProjectPath(folder);
        var temporary = target + ".tmp";
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(temporary, ProjectJson.Serialize(document), encoding);
        File.Move(temporary, target, overwrite: true);
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
            return ProjectLoader.Load(DecodeUtf8(File.ReadAllBytes(path)));
        }
        catch (DecoderFallbackException)
        {
            return ProjectLoadResult.Invalid(Strings.NotUtf8());
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

    private static string DecodeUtf8(byte[] bytes)
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return encoding.GetString(bytes, 3, bytes.Length - 3);
        }

        return encoding.GetString(bytes);
    }
}
