using FluentAssertions;
using Labz.Core.Project;
using Labz.Core.Units;
using Labz.Modules.Circuits;

namespace Labz.Modules.Circuits.Tests;

public class QaFixTests
{
    [Fact]
    public void Duplicate_json_key_is_an_invalid_file()
    {
        const string json = """
            {
              "schemaVersion": "0.1",
              "schemaVersion": "0.1",
              "id": "x",
              "name": "n",
              "createdAt": "2026-01-01T00:00:00Z",
              "idea": "",
              "modules": { "circuits": { "components": [], "nets": [], "usageProfiles": [], "analyses": [] } },
              "history": []
            }
            """;

        var act = () => ProjectLoader.Load(json);
        var result = act.Should().NotThrow().Subject;
        result.Status.Should().Be(ProjectLoadStatus.Invalid);
        result.Error.Should().Be("O arquivo do projeto não é válido.");
    }

    [Fact]
    public void Non_finite_layout_is_rejected_before_save_can_crash()
    {
        var json = MinimalProject().Replace("\"x\": 0", "\"x\": 1e400", StringComparison.Ordinal);
        var result = ProjectLoader.Load(json);
        result.Status.Should().Be(ProjectLoadStatus.Invalid);
        result.Error.Should().Contain("finito");

        var folder = TempFolder();
        ProjectStore.Save(folder, ValidDocument());
        File.Exists(Path.Combine(folder, "project.labz.json.tmp")).Should().BeFalse();
        ProjectStore.Open(folder).Status.Should().Be(ProjectLoadStatus.Success);
    }

    [Fact]
    public void Garbage_version_is_invalid_and_rotation_and_hours_are_bounded()
    {
        var garbage = MinimalProject().Replace("\"schemaVersion\": \"0.1\"", "\"schemaVersion\": \"abc\"", StringComparison.Ordinal);
        ProjectLoader.Load(garbage).Status.Should().Be(ProjectLoadStatus.Invalid);
        ProjectLoader.Load(garbage).Error.Should().NotContain("versão mais nova");

        var tilted = MinimalProject().Replace("\"rotation\": 0", "\"rotation\": 45", StringComparison.Ordinal);
        ProjectLoader.Load(tilted).Error.Should().Contain("270");

        var longDay = MinimalProject().Replace("\"hoursPerDay\": 6", "\"hoursPerDay\": 25", StringComparison.Ordinal);
        ProjectLoader.Load(longDay).Error.Should().Contain("24");
    }

    [Fact]
    public void Utf8_rejection_names_the_encoding()
    {
        var folder = TempFolder();
        File.WriteAllBytes(Path.Combine(folder, ProjectStore.FileName), [0xFF, 0xFE, 0x00]);
        var opened = ProjectStore.Open(folder);
        opened.Status.Should().Be(ProjectLoadStatus.Invalid);
        opened.Error.Should().Contain("UTF-8");
    }

    [Fact]
    public void Negative_resistance_uses_the_portuguese_label()
    {
        var session = Blank();
        var id = session.Place("resistor", 0, 0);
        session.TrySetParam(id, "resistance", "-4,7 Ω", out var error).Should().BeFalse();
        error.Should().Contain("Resistência");
        session.State.Find(id)!.Params["resistance"].Should().Be("1 kΩ");
    }

    [Fact]
    public void Usage_hours_above_24_use_the_invalid_hours_message()
    {
        var session = Blank();
        var act = () => session.SetUsageHours("LOAD", 30);
        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*horas*");
    }

    [Fact]
    public void Round_trip_keeps_rotation_and_usage_hours()
    {
        var session = Blank();
        var id = session.Place("resistor", 40, 60);
        session.Rotate(id);
        session.Rotate(id);
        session.SetUsageHours("LOAD", 6);
        var document = session.ToDocument();
        document.Modules.Circuits.Components.Single().Layout.Rotation.Should().Be(180);
        document.Modules.Circuits.UsageProfiles.Single().HoursPerDay.Should().Be(6);
        var again = SchematicSession.FromDocument(document);
        again.State.Find(id)!.Rotation.Should().Be(180);
        CircuitMapper.ToEnergy(again.State).LoadHoursPerDay.Should().Be(6);
    }

    [Fact]
    public void Merging_two_wired_nets_keeps_every_pin_and_ground_stays_zero()
    {
        var session = Blank();
        var ground = session.Place("ground", 0, 200);
        session.State.Nets.Should().ContainSingle(net => net.Id == "0" && net.Connections.Any(pin => pin.ComponentId == ground));
        var first = session.Place("resistor", 0, 0);
        var second = session.Place("resistor", 120, 0);
        var third = session.Place("resistor", 240, 0);
        var fourth = session.Place("resistor", 360, 0);
        session.ConnectPins(first, "2", second, "1");
        session.ConnectPins(third, "1", fourth, "2");
        session.ConnectPins(second, "1", third, "1");
        var merged = session.State.Nets.Should().ContainSingle(net => net.Connections.Count == 4).Subject;
        merged.Connections.Should().Contain(pin => pin.ComponentId == first && pin.Pin == "2");
        merged.Connections.Should().Contain(pin => pin.ComponentId == fourth && pin.Pin == "2");

        session.ConnectPins(second, "1", ground, "g");
        var grounded = session.State.Nets.Single(net => net.Connections.Any(pin => pin.ComponentId == ground));
        grounded.Id.Should().Be("0");
        grounded.Connections.Should().HaveCount(5);
    }

    [Fact]
    public void A_drag_is_one_history_entry_and_snaps_to_the_grid()
    {
        var session = Blank();
        var id = session.Place("resistor", 0, 0);
        var before = session.State.History.Count;
        session.BeginDrag(id);
        session.PreviewDrag(id, 14, 26);
        session.PreviewDrag(id, 33, 41);
        session.PreviewDrag(id, 48, 52);
        session.CommitDrag(id);
        session.State.Find(id)!.X.Should().Be(40);
        session.State.Find(id)!.Y.Should().Be(60);
        session.State.History.Count.Should().Be(before + 1);
        session.Undo();
        session.State.Find(id)!.X.Should().Be(0);
    }

    [Fact]
    public void Redo_is_cleared_by_a_new_edit()
    {
        var session = Blank();
        var id = session.Place("resistor", 0, 0);
        session.Undo();
        session.Place("capacitor", 80, 0);
        session.Redo().Should().BeFalse();
        session.State.Find(id).Should().BeNull();
    }

    [Fact]
    public void Fifteen_ohm_rows_say_excede_and_a_bad_balance_is_warned()
    {
        var session = SchematicSession.FromDocument(Ref1Sample.Load());
        session.SetSwitchClosed("S1", false);
        session.SetSwitchClosed("S2", true);
        foreach (var resistor in session.State.Components.Where(component => component.Type == "resistor"))
        {
            session.TrySetParam(resistor.Id, "resistance", "15 Ω", out _).Should().BeTrue();
        }

        var solution = new DcSolver().Solve(CircuitMapper.ToCircuit(session.State), CircuitMapper.ToEnergy(session.State));
        var projection = ResultProjection.From(session.State, solution, 6);
        projection.Rows.Where(row => row.Id.StartsWith('R')).Should().OnlyContain(row => row.RatingText == "excede a potência");

        var unbalanced = new DcSolution(solution.Results, false, solution.PowerRatings);
        ResultProjection.From(session.State, unbalanced, 6).BalanceText.Should().Be("Balanço de potência fora da tolerância.");
    }

    [Fact]
    public void Scenario_B_noise_is_zero_and_scenario_C_splits_energy_cards()
    {
        QuantityFormatter.Format(new Quantity(1.77636e-15, Dimension.Voltage)).Should().NotContain("f");
        var noise = new Labz.Core.Engine.EngineResult("S2.V", 1.77636e-15, "V", "mna.dc", []);
        ResultText.Format(noise, 6, 12).Should().Be("0 V");

        var session = SchematicSession.FromDocument(Ref1Sample.Load());
        session.SetSwitchClosed("S1", true);
        session.SetSwitchClosed("S2", false);
        var day = ResultProjection.From(session.State, Solve(session), 6);
        day.Rows.Select(row => row.Voltage).Should().NotContain(text => text.Contains('f'));

        session.SetSwitchClosed("S2", true);
        var both = Solve(session);
        var cards = ResultProjection.From(session.State, both, 6);
        cards.Cards.Single(card => card.Title == "Consumo").Text.Should().Contain("Wh/dia");
        cards.Cards.Single(card => card.Title == "Carga").Trace.Should().Contain("energy.charge.Wh_day");
        cards.Cards.Single(card => card.Title == "Autonomia").Text.Should().NotBe("—").And.Contain("dia");
    }

    [Fact]
    public void Parallel_voltage_sources_are_a_short()
    {
        var circuit = new CircuitBuilder()
            .Add(new DcVoltageSourceComponent("V1", 5))
            .Add(new DcVoltageSourceComponent("V2", 3))
            .Add(new GroundComponent("GND"))
            .Connect("N", ("V1", "+"), ("V2", "+"))
            .Connect("0", ("V1", "-"), ("V2", "-"), ("GND", "g"))
            .Build();
        CircuitDiagnostics.Check(circuit).Should().Contain(item =>
            item.Kind == CircuitProblemKind.VoltageSourceShort
            && item.ComponentIds.Contains("V1")
            && item.ComponentIds.Contains("V2"));
    }

    [Fact]
    public void Solver_exceptions_stop_the_run_with_the_right_component()
    {
        var session = Blank();
        var source = session.Place("dc_voltage_source", 0, 0);
        var resistor = session.Place("resistor", 120, 0);
        var ground = session.Place("ground", 0, 80);
        session.TrySetParam(source, "voltage", "5 V", out _);
        session.ConnectPins(source, "+", resistor, "1");
        session.ConnectPins(resistor, "2", ground, "g");
        session.ConnectPins(source, "-", ground, "g");
        var missing = new LiveSolveController(
            solve: (_, _, _, _) => throw new MissingGroundException("falta terra", ["R1"]));
        missing.SolveNow(session.State);
        missing.IsRunning.Should().BeFalse();
        missing.ShowProgress.Should().BeFalse();
        missing.BlockedMessage.Should().NotBeNull();
        missing.Problems.Should().ContainSingle(problem =>
            problem.Kind == CircuitProblemKind.MissingGround && problem.ComponentIds.Contains("R1"));

        var boom = new LiveSolveController(
            solve: (_, _, _, _) => throw new InvalidOperationException("falhou"));
        boom.SolveNow(session.State);
        boom.IsRunning.Should().BeFalse();
        boom.ShowProgress.Should().BeFalse();
        boom.BlockedMessage.Should().NotBeNull();
        boom.Problems.Should().Contain(problem => problem.Kind == CircuitProblemKind.CalculationFailed);
    }

    [Fact]
    public void Equal_timestamps_prune_the_older_suffix_first()
    {
        var folder = TempFolder();
        var document = ValidDocument();
        var instant = DateTimeOffset.UnixEpoch;
        SnapshotStore.Write(folder, document, instant);
        SnapshotStore.Write(folder, document, instant);
        for (var second = 1; second <= 19; second++)
        {
            SnapshotStore.Write(folder, document, instant.AddSeconds(second));
        }

        var names = SnapshotStore.List(folder).Select(Path.GetFileName).ToArray();
        names.Should().HaveCount(20);
        names.Should().NotContain("197001010000000000000.json");
        names[0].Should().Be("197001010000000000000-1.json");
    }

    [Fact]
    public void One_bad_settings_field_does_not_reset_the_other()
    {
        var path = Path.Combine(TempFolder(), "settings.json");
        File.WriteAllText(path, """{ "autosaveIntervalMinutes": 99, "significantDigits": 5 }""");
        var settings = UserSettingsStore.Load(path);
        settings.AutosaveIntervalMinutes.Should().Be(2);
        settings.SignificantDigits.Should().Be(5);
    }

    [Fact]
    public void Result_screens_bind_voltage_current_and_rating_traces()
    {
        var root = RepoRoot();
        var results = File.ReadAllText(Path.Combine(root, "src", "Labz.App", "Views", "ResultsView.xaml"));
        var schematic = File.ReadAllText(Path.Combine(root, "src", "Labz.App", "Views", "SchematicView.xaml"));
        foreach (var source in new[] { results, schematic })
        {
            source.Should().Contain("ToolTip=\"{Binding VoltageTrace}\"");
            source.Should().Contain("ToolTip=\"{Binding CurrentTrace}\"");
            source.Should().Contain("ToolTip=\"{Binding RatingTrace}\"");
        }

        var ui = File.ReadAllText(Path.Combine(root, "src", "Labz.App", "Resources", "Strings.resx"));
        ui.Should().NotContain("canvas");
        ui.Should().NotContain("Preset Schottky");
        ui.Should().Contain("Predefinição Schottky");
        ui.Should().Contain("Clique no esquema");
    }

    private static DcSolution Solve(SchematicSession session) =>
        new DcSolver().Solve(CircuitMapper.ToCircuit(session.State), CircuitMapper.ToEnergy(session.State));

    private static SchematicSession Blank() =>
        SchematicSession.CreateBlank("Teste", new DateTimeOffset(2026, 10, 18, 16, 0, 0, TimeSpan.Zero));

    private static ProjectDocument ValidDocument() => new()
    {
        Id = "ok",
        Name = "Ok",
        CreatedAt = DateTimeOffset.UnixEpoch,
        Modules = new ProjectModules
        {
            Circuits = new CircuitDocument
            {
                UsageProfiles = [new UsageProfileDocument { Target = "LOAD", HoursPerDay = 6 }],
            },
        },
    };

    private static string MinimalProject() => """
        {
          "schemaVersion": "0.1",
          "id": "x",
          "name": "n",
          "createdAt": "2026-01-01T00:00:00Z",
          "idea": "",
          "modules": {
            "circuits": {
              "components": [
                { "id": "R1", "type": "resistor", "params": { "resistance": "1 kΩ", "powerRating": "0,25 W" }, "pins": ["1", "2"], "layout": { "x": 0, "y": 0, "rotation": 0 } }
              ],
              "nets": [],
              "usageProfiles": [ { "target": "LOAD", "hoursPerDay": 6 } ],
              "analyses": []
            }
          },
          "history": []
        }
        """;

    private static string TempFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "labz-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string RepoRoot()
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

        throw new InvalidOperationException("Could not find the repository root.");
    }
}
