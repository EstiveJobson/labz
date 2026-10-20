using System.Text.Json.Nodes;
using FluentAssertions;
using Labz.Core.Project;

namespace Labz.Core.Project.Tests;

public class ProjectFileTests
{
    [Fact]
    public void Ref1_round_trip_preserves_the_model()
    {
        var original = LoadSample();
        var folder = TempFolder();

        ProjectStore.Save(folder, original);
        var opened = ProjectStore.Open(folder);

        opened.Status.Should().Be(ProjectLoadStatus.Success);
        opened.Migrated.Should().BeFalse();
        Equivalent(opened.Document!, original).Should().BeTrue();
        Directory.Exists(Path.Combine(folder, ProjectStore.ResultsDirectoryName)).Should().BeTrue();
        Directory.Exists(Path.Combine(folder, ProjectStore.HistoryDirectoryName)).Should().BeTrue();
    }

    [Fact]
    public void Saved_project_file_has_no_results_payload()
    {
        var folder = TempFolder();
        ProjectStore.Save(folder, LoadSample());
        Directory.CreateDirectory(Path.Combine(folder, ProjectStore.ResultsDirectoryName));
        File.WriteAllText(Path.Combine(folder, ProjectStore.ResultsDirectoryName, "op.json"), """{"balance.P":0}""");

        var json = File.ReadAllText(ProjectStore.ProjectPath(folder));
        var root = JsonNode.Parse(json)!.AsObject();
        ContainsProperty(root, "results").Should().BeFalse();
        json.Should().NotContain("chargeEfficiency");
        root["schemaVersion"]!.GetValue<string>().Should().Be(ProjectVersions.Current);

        var reopened = ProjectStore.Open(folder);
        reopened.Status.Should().Be(ProjectLoadStatus.Success);
        reopened.Document!.Modules.Circuits.UsageProfiles.Select(profile => profile.Target)
            .Should().Equal("LOAD", "PV1");
    }

    [Fact]
    public void Ref1_sample_matches_section_9_1()
    {
        var document = LoadSample();
        var circuit = document.Modules.Circuits;
        document.SchemaVersion.Should().Be("0.1");
        document.Name.Should().NotBeNullOrWhiteSpace();
        document.Idea.Should().NotBeNullOrWhiteSpace();

        circuit.Components.Should().HaveCount(45);
        circuit.Components.Count(component => component.Type == "resistor").Should().Be(10);
        circuit.Components.Count(component => component.Type == "led").Should().Be(30);
        circuit.Components.Should().ContainSingle(component => component.Id == "PV1" && component.Type == "solar_panel");
        circuit.Components.Should().ContainSingle(component => component.Id == "BAT1" && component.Type == "battery");
        circuit.Components.Should().ContainSingle(component => component.Id == "GND" && component.Type == "ground");

        var panel = circuit.Components.Single(component => component.Id == "PV1");
        panel.Params["openCircuitVoltage"].Should().Be("18 V");
        panel.Params["seriesResistance"].Should().Be("5 Ω");
        panel.Params["ratedPower"].Should().Be("20 W");

        var battery = circuit.Components.Single(component => component.Id == "BAT1");
        battery.Params["nominalVoltage"].Should().Be("12 V");
        battery.Params["internalResistance"].Should().Be("0,1 Ω");
        battery.Params["capacity"].Should().Be("7 Ah");
        battery.Params["usableDepthOfDischarge"].Should().Be("0,5");

        circuit.Components.Single(component => component.Id == "S1").Params["state"].Should().Be("open");
        circuit.Components.Single(component => component.Id == "S2").Params["state"].Should().Be("closed");
        circuit.Components.Where(component => component.Type == "resistor").Should()
            .OnlyContain(component => component.Params["resistance"] == "150 Ω" && component.Params["powerRating"] == "0,25 W");
        circuit.Components.Where(component => component.Type == "led").Should()
            .OnlyContain(component =>
                component.Params["forwardVoltage"] == "3 V"
                && component.Params["forwardCurrent"] == "20 mA"
                && component.Params["color"] == "branco");

        for (var branch = 1; branch <= 10; branch++)
        {
            circuit.Components.Should().Contain(component => component.Id == $"R{branch}");
            circuit.Components.Should().Contain(component => component.Id == $"L{branch}A");
            circuit.Components.Should().Contain(component => component.Id == $"L{branch}B");
            circuit.Components.Should().Contain(component => component.Id == $"L{branch}C");
        }

        circuit.UsageProfiles.Should().ContainSingle(profile => profile.Target == "LOAD" && profile.HoursPerDay == 6);
        circuit.UsageProfiles.Should().ContainSingle(profile => profile.Target == "PV1" && profile.HoursPerDay == 5);
        circuit.Analyses.Should().ContainSingle(analysis => analysis.Type == "op");
        circuit.Nets.Should().Contain(net => net.Id == "0" && net.Connections.Any(pin => pin.ComponentId == "GND" && pin.Pin == "g"));
        circuit.Nets.Should().Contain(net => net.Id == "N_BAT");
        circuit.Nets.Should().HaveCount(34);

        var centers = circuit.Components.Select(component => (component.Layout.X, component.Layout.Y)).ToArray();
        foreach (var (x, y) in centers)
        {
            (x % 20).Should().Be(0);
            (y % 20).Should().Be(0);
        }

        for (var left = 0; left < centers.Length; left++)
        {
            for (var right = left + 1; right < centers.Length; right++)
            {
                var dx = centers[left].X - centers[right].X;
                var dy = centers[left].Y - centers[right].Y;
                Math.Sqrt((dx * dx) + (dy * dy)).Should().BeGreaterThanOrEqualTo(100);
            }
        }
    }

    [Theory]
    [InlineData("0.2")]
    [InlineData("1.0")]
    [InlineData("9")]
    public void Newer_schema_version_returns_a_portuguese_error_without_throwing(string version)
    {
        var json = SampleJson().Replace("\"schemaVersion\": \"0.1\"", $"\"schemaVersion\": \"{version}\"", StringComparison.Ordinal);
        var result = ProjectLoader.Load(json);

        result.Status.Should().Be(ProjectLoadStatus.NewerVersion);
        result.Document.Should().BeNull();
        result.Error.Should().StartWith("Este projeto foi criado por uma versão mais nova do LABZ");
    }

    [Fact]
    public void Older_schema_version_is_migrated()
    {
        const string json = """
            {
              "schemaVersion": "0.0",
              "id": "legacy-1",
              "name": "Antigo",
              "createdAt": "2026-01-01T00:00:00Z",
              "idea": "versao antiga",
              "circuit": {
                "components": [
                  {
                    "id": "R1",
                    "type": "resistor",
                    "params": { "resistance": "4,7 kΩ", "powerRating": "0,25 W" },
                    "pins": ["1", "2"],
                    "layout": { "x": 40, "y": 80, "rotation": 90 }
                  }
                ],
                "nets": [],
                "usageProfiles": [ { "target": "LOAD", "hoursPerDay": 6 } ]
              }
            }
            """;

        var result = ProjectLoader.Load(json);

        result.Status.Should().Be(ProjectLoadStatus.Success);
        result.Migrated.Should().BeTrue();
        result.Error.Should().BeNull();
        var document = result.Document!;
        document.SchemaVersion.Should().Be(ProjectVersions.Current);
        document.History.Should().BeEmpty();
        document.Modules.Circuits.Analyses.Should().BeEmpty();
        document.Modules.Circuits.Components.Should().ContainSingle();
        document.Modules.Circuits.Components[0].Params["resistance"].Should().Be("4,7 kΩ");
        document.Modules.Circuits.UsageProfiles.Should().ContainSingle(profile => profile.Target == "LOAD" && profile.HoursPerDay == 6);

        var folder = TempFolder();
        ProjectStore.Save(folder, document);
        var reopened = ProjectStore.Open(folder);
        reopened.Migrated.Should().BeFalse();
        Equivalent(reopened.Document!, document).Should().BeTrue();
    }

    [Fact]
    public void Migration_pipeline_exposes_the_legacy_step()
    {
        ProjectMigrationPipeline.Migrations.Should().ContainSingle(step =>
            step.FromVersion == ProjectVersions.Legacy && step.ToVersion == ProjectVersions.Current);
    }

    [Theory]
    [InlineData("""{ "schemaVersion": "0.1", "id": "x", "name": "n", "createdAt": "2026-01-01T00:00:00Z", "idea": "", "modules": { "circuits": { "components": [], "nets": [], "usageProfiles": [], "analyses": [] } }, "history": [], "results": [] }""")]
    [InlineData("""{ "schemaVersion": "0.1", "id": "x", "name": "n", "createdAt": "2026-01-01T00:00:00Z", "idea": "", "modules": { "circuits": { "components": [ { "id": "Q1", "type": "widget", "params": {}, "pins": [], "layout": { "x": 0, "y": 0, "rotation": 0 } } ], "nets": [], "usageProfiles": [], "analyses": [] } }, "history": [] }""")]
    [InlineData("{ this is not json")]
    [InlineData("""{ "id": "x", "name": "n" }""")]
    public void Schema_invalid_file_returns_a_portuguese_error_without_throwing(string json)
    {
        var result = ProjectLoader.Load(json);

        result.Status.Should().Be(ProjectLoadStatus.Invalid);
        result.Document.Should().BeNull();
        result.Error.Should().Be("O arquivo do projeto não é válido.");
    }

    [Fact]
    public void Duplicate_component_id_is_rejected_in_portuguese()
    {
        const string json = """
            {
              "schemaVersion": "0.1",
              "id": "dup",
              "name": "Dup",
              "createdAt": "2026-01-01T00:00:00Z",
              "idea": "",
              "modules": {
                "circuits": {
                  "components": [
                    { "id": "R1", "type": "resistor", "params": {}, "pins": ["1", "2"], "layout": { "x": 0, "y": 0, "rotation": 0 } },
                    { "id": "R1", "type": "resistor", "params": {}, "pins": ["1", "2"], "layout": { "x": 120, "y": 0, "rotation": 0 } }
                  ],
                  "nets": [],
                  "usageProfiles": [],
                  "analyses": []
                }
              },
              "history": []
            }
            """;

        var result = ProjectLoader.Load(json);

        result.Status.Should().Be(ProjectLoadStatus.Invalid);
        result.Error.Should().Contain("R1");
    }

    private static ProjectDocument LoadSample()
    {
        var result = ProjectLoader.Load(SampleJson());
        result.Status.Should().Be(ProjectLoadStatus.Success, result.Error);
        return result.Document!;
    }

    private static string SampleJson() => File.ReadAllText(SamplePath());

    private static string SamplePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "samples", "REF-1-solar-led", "project.labz.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find samples/REF-1-solar-led/project.labz.json.");
    }

    private static string TempFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "labz-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static bool ContainsProperty(JsonObject root, string name)
    {
        if (root.ContainsKey(name))
        {
            return true;
        }

        foreach (var property in root)
        {
            if (property.Value is JsonObject child && ContainsProperty(child, name))
            {
                return true;
            }

            if (property.Value is JsonArray array && array.OfType<JsonObject>().Any(child => ContainsProperty(child, name)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Equivalent(ProjectDocument left, ProjectDocument right)
    {
        if (left.SchemaVersion != right.SchemaVersion
            || left.Id != right.Id
            || left.Name != right.Name
            || left.Idea != right.Idea
            || !left.CreatedAt.EqualsExact(right.CreatedAt)
            || left.History.Count != right.History.Count)
        {
            return false;
        }

        for (var index = 0; index < left.History.Count; index++)
        {
            var a = left.History[index];
            var b = right.History[index];
            if (a.Source != b.Source || a.Change != b.Change || !a.At.EqualsExact(b.At))
            {
                return false;
            }
        }

        return Equivalent(left.Modules.Circuits, right.Modules.Circuits);
    }

    private static bool Equivalent(CircuitDocument left, CircuitDocument right)
    {
        if (left.Components.Count != right.Components.Count
            || left.Nets.Count != right.Nets.Count
            || left.UsageProfiles.Count != right.UsageProfiles.Count
            || left.Analyses.Count != right.Analyses.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Components.Count; index++)
        {
            if (!Equivalent(left.Components[index], right.Components[index]))
            {
                return false;
            }
        }

        for (var index = 0; index < left.Nets.Count; index++)
        {
            var a = left.Nets[index];
            var b = right.Nets[index];
            if (a.Id != b.Id || a.Connections.Count != b.Connections.Count)
            {
                return false;
            }

            for (var pin = 0; pin < a.Connections.Count; pin++)
            {
                if (a.Connections[pin].ComponentId != b.Connections[pin].ComponentId
                    || a.Connections[pin].Pin != b.Connections[pin].Pin)
                {
                    return false;
                }
            }
        }

        for (var index = 0; index < left.UsageProfiles.Count; index++)
        {
            if (left.UsageProfiles[index].Target != right.UsageProfiles[index].Target
                || left.UsageProfiles[index].HoursPerDay != right.UsageProfiles[index].HoursPerDay)
            {
                return false;
            }
        }

        for (var index = 0; index < left.Analyses.Count; index++)
        {
            if (left.Analyses[index].Type != right.Analyses[index].Type
                || left.Analyses[index].Step != right.Analyses[index].Step
                || left.Analyses[index].Stop != right.Analyses[index].Stop)
            {
                return false;
            }
        }

        return true;
    }

    private static bool Equivalent(ComponentDocument left, ComponentDocument right)
    {
        if (left.Id != right.Id
            || left.Type != right.Type
            || left.Layout.X != right.Layout.X
            || left.Layout.Y != right.Layout.Y
            || left.Layout.Rotation != right.Layout.Rotation
            || !left.Pins.SequenceEqual(right.Pins)
            || left.Params.Count != right.Params.Count)
        {
            return false;
        }

        foreach (var (key, value) in left.Params)
        {
            if (!right.Params.TryGetValue(key, out var other) || other != value)
            {
                return false;
            }
        }

        return true;
    }
}
