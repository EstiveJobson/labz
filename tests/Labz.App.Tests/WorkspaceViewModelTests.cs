using System.IO;
using FluentAssertions;
using Labz.App;
using Labz.App.ViewModels;
using Labz.Core.Project;
using Labz.Modules.Circuits;

namespace Labz.App.Tests;

public class WorkspaceViewModelTests
{
    [Fact]
    public void Home_commands_save_open_ref1_and_honor_the_discard_prompt()
    {
        var clock = new FakeClock(DateTimeOffset.Parse("2026-10-18T16:00:00Z"));
        var dialogs = new FakeDialogs();
        var root = Path.Combine(Path.GetTempPath(), "labz-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var main = new MainViewModel(
            SchematicSession.CreateBlank("Sem título", clock.Now),
            new DirectMainThread(),
            dialogs,
            Path.Combine(root, "recent.json"),
            Path.Combine(root, "settings.json"),
            clock);

        main.Home.IdeaEnabled.Should().BeFalse();
        main.Home.IdeaHint.Should().Be("Disponível quando o Assistente chegar");
        main.Home.NewFromIdeaLabel.Should().Be("Novo projeto a partir de uma ideia");

        var resistor = main.Schematic.Session.Place("resistor", 0, 0);
        main.IsDirty.Should().BeTrue();
        main.Schematic.Undo();
        main.Schematic.Session.State.Components.Should().BeEmpty();
        main.Schematic.Redo();
        main.Schematic.Session.State.Find(resistor).Should().NotBeNull();

        dialogs.ConfirmResult = false;
        main.NewProject();
        main.Schematic.Session.State.Find(resistor).Should().NotBeNull();

        dialogs.ConfirmResult = true;
        var folder = Path.Combine(root, "saved");
        dialogs.Folders.Enqueue(folder);
        main.Save();
        File.Exists(Path.Combine(folder, ProjectStore.FileName)).Should().BeTrue();
        main.IsDirty.Should().BeFalse();
        main.LoadRecent().Should().ContainSingle(project => project.Folder == folder);

        main.OpenRef1();
        main.Session.State.Components.Should().HaveCount(45);
        main.IsDirty.Should().BeTrue();
        main.Folder.Should().BeNull();

        main.Preferences.AutosaveText = "0";
        main.Preferences.Error.Should().Contain("1 e 60");
        main.Settings.AutosaveIntervalMinutes.Should().Be(2);
        main.Preferences.AutosaveText = "15";
        main.Preferences.Error.Should().BeNull();
        main.Settings.AutosaveIntervalMinutes.Should().Be(15);
        UserSettingsStore.Load(main.SettingsPath).AutosaveIntervalMinutes.Should().Be(15);

        dialogs.Folders.Enqueue(Path.Combine(root, "ref1-copy"));
        main.Save();
        main.IsDirty.Should().BeFalse();
        main.Schematic.Session.TrySetParam("R1", "resistance", "15 Ω", out _).Should().BeTrue();
        main.IsDirty.Should().BeTrue();
        clock.Now = clock.Now.AddMinutes(15);
        main.PulseAutosave().Should().BeTrue();
        SnapshotStore.List(main.Folder!).Should().ContainSingle();
        main.PulseAutosave().Should().BeFalse();
    }

    private sealed class FakeDialogs : IUserDialogs
    {
        public Queue<string?> Folders { get; } = new();

        public bool ConfirmResult { get; set; } = true;

        public bool Confirm(string message) => ConfirmResult;

        public string? ChooseFolder(string title) => Folders.Count == 0 ? null : Folders.Dequeue();

        public void Alert(string message)
        {
        }
    }

    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
