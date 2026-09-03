using LogBackup.Core.Backup;
using LogBackup.Core.Models;

namespace LogBackup.Tests;

public class RetentionTests
{
    [Fact]
    public async Task KeepCountDeletesOldestBeyondLimit()
    {
        using var h = new TestHarness();
        var source = new BackupSourceConfig { Path = h.SourceDir, Recursive = true };

        var ids = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            h.WriteSourceFile("app.log", $"version {i}");
            var result = await h.BackupEngine.CreateBackupAsync(source, h.KeyId, BackupMode.Full);
            ids.Add(result.Metadata.BackupId);
            await Task.Delay(1100); // backup id has 1-second resolution; force distinct ids/ordering
        }

        var report = await h.RetentionEngine.ApplyAsync(new RetentionConfig { KeepCount = 1 });

        Assert.Equal(2, report.Deleted.Count);
        Assert.Single(report.Preserved);
        Assert.Equal(ids[^1], report.Preserved[0]);
    }

    [Fact]
    public async Task LockedBackupIsPreservedByRetention()
    {
        using var h = new TestHarness();
        var source = new BackupSourceConfig { Path = h.SourceDir, Recursive = true };
        h.WriteSourceFile("app.log", "content");
        var result = await h.BackupEngine.CreateBackupAsync(source, h.KeyId, BackupMode.Full);

        await h.MetadataStore.SetLockedAsync(result.Metadata.BackupId, true);

        var report = await h.RetentionEngine.ApplyAsync(new RetentionConfig { KeepCount = 0 });

        Assert.Empty(report.Deleted);
        Assert.Single(report.SkippedLocked);
        Assert.NotNull(await h.MetadataStore.GetAsync(result.Metadata.BackupId));
    }

    [Fact]
    public async Task DryRunDoesNotDeleteAnything()
    {
        using var h = new TestHarness();
        var source = new BackupSourceConfig { Path = h.SourceDir, Recursive = true };
        h.WriteSourceFile("app.log", "content");
        var result = await h.BackupEngine.CreateBackupAsync(source, h.KeyId, BackupMode.Full);

        var report = await h.RetentionEngine.ApplyAsync(new RetentionConfig { KeepCount = 0 }, dryRun: true);

        Assert.Single(report.Deleted);
        Assert.NotNull(await h.MetadataStore.GetAsync(result.Metadata.BackupId));
        Assert.True(await h.Storage.ExistsAsync(result.Metadata.ArtifactFileName));
    }
}
