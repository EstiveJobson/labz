using FluentAssertions;
using Labz.Core.Project;

namespace Labz.Core.Project.Tests;

public class WorkspaceStorageTests
{
    [Fact]
    public void Recent_projects_keep_the_last_ten()
    {
        var path = Path.Combine(TempFolder(), "recent.json");
        for (var index = 0; index < 12; index++)
        {
            RecentProjectStore.Remember(path, Path.Combine("projects", index.ToString()), "Projeto " + index, DateTimeOffset.UnixEpoch.AddMinutes(index));
        }

        var again = RecentProjectStore.Remember(path, Path.Combine("projects", "3"), "Projeto 3", DateTimeOffset.UnixEpoch.AddHours(2));
        again.Should().HaveCount(10);
        again[0].Folder.Should().Be(Path.Combine("projects", "3"));
        again.Select(item => item.Folder).Should().OnlyHaveUniqueItems();
        RecentProjectStore.Load(path).Should().HaveCount(10);
    }

    [Fact]
    public void Autosave_keeps_the_last_twenty_snapshots()
    {
        var folder = TempFolder();
        var document = new ProjectDocument
        {
            Id = "snap",
            Name = "Snap",
            CreatedAt = DateTimeOffset.UnixEpoch,
            Modules = new ProjectModules(),
        };
        for (var index = 0; index < 25; index++)
        {
            SnapshotStore.Write(folder, document, DateTimeOffset.UnixEpoch.AddSeconds(index));
        }

        var files = SnapshotStore.List(folder);
        files.Should().HaveCount(20);
        Path.GetFileName(files[0]).Should().Be("197001010000050000000.json");
        Path.GetFileName(files[^1]).Should().Be("197001010000240000000.json");
    }

    [Fact]
    public void Autosave_interval_uses_the_clock_and_rejects_values_outside_1_to_60()
    {
        var clock = new FakeClock(DateTimeOffset.Parse("2026-10-18T16:00:00Z"));
        var minutes = 2;
        var policy = new AutosavePolicy(clock, () => minutes);
        policy.IsDue().Should().BeFalse();
        clock.Now = clock.Now.AddMinutes(1);
        policy.IsDue().Should().BeFalse();
        clock.Now = clock.Now.AddMinutes(1);
        policy.IsDue().Should().BeTrue();
        policy.MarkSaved();
        policy.IsDue().Should().BeFalse();
        minutes = 1;
        clock.Now = clock.Now.AddMinutes(1);
        policy.IsDue().Should().BeTrue();

        UserSettings.TryAutosaveMinutes(1, out _).Should().BeTrue();
        UserSettings.TryAutosaveMinutes(60, out _).Should().BeTrue();
        UserSettings.TryAutosaveMinutes(0, out var low).Should().BeFalse();
        UserSettings.TryAutosaveMinutes(61, out var high).Should().BeFalse();
        low.Should().Be("O intervalo de salvamento automático deve ficar entre 1 e 60 minutos.");
        high.Should().Be(low);
        UserSettings.TrySignificantDigits(6, out _).Should().BeTrue();
        UserSettings.TrySignificantDigits(0, out var digits).Should().BeFalse();
        digits.Should().Contain("1 e 8");
    }

    private static string TempFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "labz-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
