using System.Reflection;
using System.Xml.Linq;
using FluentAssertions;

namespace Labz.Architecture.Tests;

/// <summary>
/// CORE-7: core and module assemblies must not reference WPF or System.Windows.
/// </summary>
public class CoreWpfBoundaryTests
{
    private static readonly string[] CoreProjects =
    [
        "Labz.Core.Units",
        "Labz.Core.Project",
        "Labz.Core.Engine",
        "Labz.Core.Drawing",
        "Labz.Modules.Circuits",
    ];

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
        var violations = new List<string>();

        foreach (var projectName in CoreProjects)
        {
            var assembly = Assembly.Load(new AssemblyName(projectName));
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                if (IsForbiddenAssembly(reference.Name))
                {
                    violations.Add($"{projectName} references {reference.Name}");
                }
            }
        }

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Core_projects_do_not_enable_wpf_winforms_or_a_windows_target_framework()
    {
        var root = FindRepoRoot();
        var violations = new List<string>();

        foreach (var projectName in CoreProjects)
        {
            var projectPath = Path.Combine(root, "src", projectName, $"{projectName}.csproj");
            File.Exists(projectPath).Should().BeTrue("the core project {0} should exist at {1}", projectName, projectPath);

            var document = XDocument.Load(projectPath);
            foreach (var element in document.Descendants())
            {
                switch (element.Name.LocalName)
                {
                    case "UseWPF" or "UseWindowsForms" when IsTrue(element.Value):
                        violations.Add($"{projectName}: <{element.Name.LocalName}> is enabled");
                        break;
                    case "TargetFramework" or "TargetFrameworks"
                        when element.Value.Contains("-windows", StringComparison.OrdinalIgnoreCase):
                        violations.Add($"{projectName}: {element.Name.LocalName} is '{element.Value.Trim()}'");
                        break;
                    case "Reference" when IsForbiddenAssembly(AssemblyNameFromInclude(element.Attribute("Include")?.Value)):
                        violations.Add($"{projectName}: Reference Include=\"{element.Attribute("Include")?.Value}\"");
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
