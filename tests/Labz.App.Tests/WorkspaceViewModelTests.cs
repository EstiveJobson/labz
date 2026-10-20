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

    [Fact]
    public void Focused_edits_are_committed_before_save_and_close()
    {
        var dialogs = new FakeDialogs { ConfirmResult = false };
        var root = Path.Combine(Path.GetTempPath(), "labz-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var main = new MainViewModel(
            SchematicSession.CreateBlank("Teste", DateTimeOffset.UnixEpoch),
            new DirectMainThread(),
            dialogs,
            Path.Combine(root, "recent.json"),
            Path.Combine(root, "settings.json"),
            TimeProvider.System);
        var id = main.Session.Place("resistor", 0, 0);
        main.CommitFocusedEdits = () => main.Session.TrySetParam(id, "resistance", "10 Ω", out _);
        main.ConfirmClose().Should().BeFalse();
        main.IsDirty.Should().BeTrue();
        main.Session.State.Find(id)!.Params["resistance"].Should().Be("10 Ω");

        var folder = Path.Combine(root, "projeto");
        dialogs.Folders.Enqueue(folder);
        dialogs.ConfirmResult = true;
        main.Save();
        File.ReadAllText(Path.Combine(folder, ProjectStore.FileName)).Should().Contain("10 Ω");
    }

    [Fact]
    public void Unsaved_work_autosaves_into_the_recovery_folder_and_io_errors_are_shown()
    {
        var clock = new FakeClock(DateTimeOffset.Parse("2026-10-18T12:00:00Z"));
        var dialogs = new FakeDialogs();
        var root = Path.Combine(Path.GetTempPath(), "labz-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var main = new MainViewModel(
            SchematicSession.CreateBlank("Teste", clock.Now),
            new DirectMainThread(),
            dialogs,
            Path.Combine(root, "recent.json"),
            Path.Combine(root, "settings.json"),
            clock);
        main.RecoveryDirectory = Path.Combine(root, "recovery");
        main.Session.Place("resistor", 0, 0);
        clock.Now = clock.Now.AddMinutes(2);
        main.PulseAutosave().Should().BeTrue();
        SnapshotStore.List(main.RecoveryDirectory).Should().ContainSingle();

        var blocker = Path.Combine(root, "not-a-folder");
        File.WriteAllText(blocker, "x");
        dialogs.Folders.Enqueue(blocker);
        main.Save();
        dialogs.Alerts.Should().Contain("Não foi possível salvar o projeto.");
    }

    [Fact]
    public void Recovery_directory_is_under_local_app_data()
    {
        var main = new MainViewModel();
        main.RecoveryDirectory.Should().Be(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Labz.App.Resources.Strings.AppTitle,
            "recovery"));
    }

    [Fact]
    public void Autosave_failure_backs_off_and_shows_one_banner()
    {
        var clock = new FakeClock(DateTimeOffset.Parse("2026-10-18T12:00:00Z"));
        var dialogs = new FakeDialogs();
        var root = Path.Combine(Path.GetTempPath(), "labz-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var main = new MainViewModel(
            SchematicSession.CreateBlank("Teste", clock.Now),
            new DirectMainThread(),
            dialogs,
            Path.Combine(root, "recent.json"),
            Path.Combine(root, "settings.json"),
            clock);
        var blocker = Path.Combine(root, "blocked");
        File.WriteAllText(blocker, "x");
        main.RecoveryDirectory = blocker;
        main.Session.Place("resistor", 0, 0);
        clock.Now = clock.Now.AddMinutes(2);
        main.PulseAutosave().Should().BeFalse();
        dialogs.Alerts.Should().BeEmpty();
        main.Notice.Should().Be("Não foi possível salvar o projeto.");

        var good = Path.Combine(root, "good");
        main.RecoveryDirectory = good;
        clock.Now = clock.Now.AddSeconds(15);
        main.PulseAutosave().Should().BeFalse();
        Directory.Exists(good).Should().BeFalse();

        clock.Now = clock.Now.AddMinutes(2);
        main.PulseAutosave().Should().BeTrue();
        SnapshotStore.List(good).Should().ContainSingle();
        main.Notice.Should().BeNull();
        dialogs.Alerts.Should().BeEmpty();
    }

    [Fact]
    public void Save_survives_a_recent_list_error()
    {
        var dialogs = new FakeDialogs();
        var root = Path.Combine(Path.GetTempPath(), "labz-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var parent = Path.Combine(root, "not-a-directory");
        File.WriteAllText(parent, "x");
        var main = new MainViewModel(
            SchematicSession.CreateBlank("Teste", DateTimeOffset.UnixEpoch),
            new DirectMainThread(),
            dialogs,
            Path.Combine(parent, "recent.json"),
            Path.Combine(root, "settings.json"),
            TimeProvider.System);
        main.Session.Place("resistor", 0, 0);
        var folder = Path.Combine(root, "projeto");
        dialogs.Folders.Enqueue(folder);
        var act = () => main.Save();
        act.Should().NotThrow();
        File.ReadAllText(Path.Combine(folder, ProjectStore.FileName)).Should().Contain("resistor");
        dialogs.Alerts.Should().Contain("Não foi possível salvar o projeto.");
    }

    private sealed class FakeDialogs : IUserDialogs
    {
        public Queue<string?> Folders { get; } = new();

        public List<string> Alerts { get; } = [];

        public bool ConfirmResult { get; set; } = true;

        public bool Confirm(string message) => ConfirmResult;

        public string? ChooseFolder(string title) => Folders.Count == 0 ? null : Folders.Dequeue();

        public void Alert(string message) => Alerts.Add(message);
    }

    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
