using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using FluentAssertions;

namespace Labz.Architecture.Tests;

/// <summary>
/// CORE-7: core and module assemblies must not reference WPF or System.Windows.
/// Projects are discovered under src (Labz.Core.* and Labz.Modules.*), not listed by name.
/// Compiled references are read from each project's build output and followed recursively,
/// so a WPF reference that arrives through an imported props file or a dependency dll is caught.
/// </summary>
public class CoreWpfBoundaryTests
{
    private const int MinimumCoreProjectCount = 5;

    private static readonly string[] ForbiddenAssemblyNames =
    [
        "PresentationFramework",
        "PresentationCore",
        "WindowsBase",
        "System.Xaml",
    ];

    private static readonly Dictionary<string, string[]> ExpectedProjectReferences = new(StringComparer.Ordinal)
    {
        ["Labz.Core.Units"] = [],
        ["Labz.Core.Project"] = ["Labz.Core.Units"],
        ["Labz.Core.Engine"] = ["Labz.Core.Units"],
        ["Labz.Core.Drawing"] = ["Labz.Core.Units"],
        ["Labz.Modules.Circuits"] =
        [
            "Labz.Core.Drawing",
            "Labz.Core.Engine",
            "Labz.Core.Project",
            "Labz.Core.Units",
        ],
        ["Labz.App"] =
        [
            "Labz.Core.Drawing",
            "Labz.Core.Engine",
            "Labz.Core.Project",
            "Labz.Core.Units",
            "Labz.Modules.Circuits",
        ],
    };

    [Fact]
    public void Core_assemblies_do_not_reference_wpf_or_system_windows()
    {
        var projects = DiscoverCoreProjects();
        projects.Should().HaveCountGreaterThanOrEqualTo(MinimumCoreProjectCount);

        var configuration = BuildConfiguration();
        var violations = new List<string>();

        foreach (var project in projects)
        {
            var builtAssemblies = FindBuiltAssemblies(project, configuration);
            if (builtAssemblies.Count == 0)
            {
                violations.Add(
                    $"{project.Name}: no built assembly under bin/{configuration}. Build the solution before running CORE-7.");
                continue;
            }

            foreach (var assemblyPath in builtAssemblies)
            {
                AddCompiledReferenceViolations(project.Name, assemblyPath, violations);
            }
        }

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Core_projects_do_not_enable_wpf_winforms_or_a_windows_target_framework()
    {
        var projects = DiscoverCoreProjects();
        projects.Should().HaveCountGreaterThanOrEqualTo(MinimumCoreProjectCount);

        var violations = new List<string>();

        foreach (var project in projects)
        {
            var document = XDocument.Load(project.ProjectPath);
            foreach (var element in document.Descendants())
            {
                switch (element.Name.LocalName)
                {
                    case "UseWPF" or "UseWindowsForms" when IsTrue(element.Value):
                        violations.Add($"{project.Name}: <{element.Name.LocalName}> is enabled");
                        break;
                    case "TargetFramework" or "TargetFrameworks"
                        when element.Value.Contains("-windows", StringComparison.OrdinalIgnoreCase):
                        violations.Add($"{project.Name}: {element.Name.LocalName} is '{element.Value.Trim()}'");
                        break;
                    case "Reference" when IsForbiddenAssembly(AssemblyNameFromInclude(element.Attribute("Include")?.Value)):
                        violations.Add($"{project.Name}: Reference Include=\"{element.Attribute("Include")?.Value}\"");
                        break;
                }
            }
        }

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Shell_ui_strings_are_pt_BR_and_come_from_resx()
    {
        var root = FindRepoRoot();
        var neutral = File.ReadAllText(Path.Combine(root, "src", "Labz.App", "Resources", "Strings.resx"));
        var brazilian = File.ReadAllText(Path.Combine(root, "src", "Labz.App", "Resources", "Strings.pt-BR.resx"));
        string[] uiText = ["LABZ", "Início", "Esquema", "Simulação", "Resultados", "Configurações", "Em construção"];

        foreach (var text in uiText)
        {
            neutral.Should().Contain(text);
            brazilian.Should().Contain(text);
        }

        File.ReadAllText(Path.Combine(root, "src", "Labz.App", "App.xaml.cs"))
            .Should()
            .Contain("pt-BR");

        var sourceFiles = Directory
            .EnumerateFiles(Path.Combine(root, "src", "Labz.App"), "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase));

        foreach (var sourceFile in sourceFiles)
        {
            var source = File.ReadAllText(sourceFile);
            foreach (var text in uiText)
            {
                source.Should().NotContain(text, "{0} must read UI text from the resx files", Path.GetRelativePath(root, sourceFile));
            }
        }
    }

    [Fact]
    public void Project_references_follow_the_architecture()
    {
        var root = FindRepoRoot();

        foreach (var (projectName, expected) in ExpectedProjectReferences)
        {
            var projectPath = Path.Combine(root, "src", projectName, $"{projectName}.csproj");
            File.Exists(projectPath).Should().BeTrue("project {0} should exist", projectName);

            ProjectReferenceNames(projectPath)
                .Should()
                .BeEquivalentTo(expected, "project {0} should reference only its architecture dependencies", projectName);
        }
    }

    private readonly record struct DiscoveredProject(string Name, string ProjectPath);

    private readonly record struct AssemblyManifest(string Name, IReadOnlyList<string> References);

    private static DiscoveredProject[] DiscoverCoreProjects()
    {
        var src = Path.Combine(FindRepoRoot(), "src");
        return Directory
            .EnumerateFiles(src, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !ContainsPathSegment(path, "bin") && !ContainsPathSegment(path, "obj"))
            .Select(path => new DiscoveredProject(Path.GetFileNameWithoutExtension(path) ?? string.Empty, path))
            .Where(project => project.Name.StartsWith("Labz.Core.", StringComparison.Ordinal)
                || project.Name.StartsWith("Labz.Modules.", StringComparison.Ordinal))
            .OrderBy(project => project.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> FindBuiltAssemblies(DiscoveredProject project, string configuration)
    {
        var projectDirectory = Path.GetDirectoryName(project.ProjectPath);
        if (projectDirectory is null)
        {
            return [];
        }

        var bin = Path.Combine(projectDirectory, "bin", configuration);
        if (!Directory.Exists(bin))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(bin, project.Name + ".dll", SearchOption.AllDirectories)
            .Where(path => !ContainsPathSegment(path, "ref"))
            .ToArray();
    }

    private static void AddCompiledReferenceViolations(string projectName, string assemblyPath, List<string> violations)
    {
        var outputDirectory = Path.GetDirectoryName(assemblyPath);
        if (outputDirectory is null)
        {
            violations.Add($"{projectName}: built assembly path '{assemblyPath}' has no directory");
            return;
        }

        if (!TryReadManifest(assemblyPath, out var rootManifest, out var failure))
        {
            violations.Add($"{projectName}: could not inspect {Path.GetFileName(assemblyPath)} ({failure})");
            return;
        }

        var manifests = new Dictionary<string, AssemblyManifest>(StringComparer.OrdinalIgnoreCase)
        {
            [rootManifest.Name] = rootManifest,
        };

        foreach (var dll in Directory.EnumerateFiles(outputDirectory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            if (TryReadManifest(dll, out var manifest, out _) && !manifests.ContainsKey(manifest.Name))
            {
                manifests.Add(manifest.Name, manifest);
            }
        }

        var pending = new Queue<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        pending.Enqueue(rootManifest.Name);

        while (pending.Count > 0)
        {
            var name = pending.Dequeue();
            if (!seen.Add(name) || !manifests.TryGetValue(name, out var manifest))
            {
                continue;
            }

            foreach (var referenceName in manifest.References)
            {
                if (IsForbiddenAssembly(referenceName))
                {
                    violations.Add(name.Equals(rootManifest.Name, StringComparison.Ordinal)
                        ? $"{projectName} references {referenceName}"
                        : $"{projectName} references {referenceName} via {name}");
                }

                if (manifests.ContainsKey(referenceName))
                {
                    pending.Enqueue(referenceName);
                }
            }
        }
    }

    private static bool TryReadManifest(string assemblyPath, out AssemblyManifest manifest, out string failure)
    {
        manifest = default;
        failure = string.Empty;

        try
        {
            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
            {
                failure = "no managed metadata";
                return false;
            }

            var reader = peReader.GetMetadataReader();
            var name = reader.GetString(reader.GetAssemblyDefinition().Name);
            var references = new List<string>();
            foreach (var handle in reader.AssemblyReferences)
            {
                references.Add(reader.GetString(reader.GetAssemblyReference(handle).Name));
            }

            manifest = new AssemblyManifest(name, references);
            return true;
        }
        catch (Exception exception) when (exception is BadImageFormatException or IOException or InvalidOperationException)
        {
            failure = $"{exception.GetType().Name}: {exception.Message}";
            return false;
        }
    }

    private static string BuildConfiguration()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (directory.Parent?.Name.Equals("bin", StringComparison.OrdinalIgnoreCase) == true)
            {
                return directory.Name;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not determine the build configuration from the test output path '{AppContext.BaseDirectory}'.");
    }

    private static bool ContainsPathSegment(string path, string segment)
    {
        return path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part.Equals(segment, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> ProjectReferenceNames(string projectPath)
    {
        var document = XDocument.Load(projectPath);
        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', Path.DirectorySeparatorChar)))
            .ToArray();
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Labz.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root containing Labz.sln.");
    }

    private static bool IsForbiddenAssembly(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (name.StartsWith("System.Windows", StringComparison.Ordinal))
        {
            return true;
        }

        return ForbiddenAssemblyNames.Contains(name, StringComparer.Ordinal);
    }

    private static string? AssemblyNameFromInclude(string? include)
    {
        if (string.IsNullOrWhiteSpace(include))
        {
            return null;
        }

        return include.Split(',')[0].Trim();
    }

    private static bool IsTrue(string value)
    {
        return value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }
}
