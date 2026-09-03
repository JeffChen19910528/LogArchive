using LogBackup.Core.Backup;
using LogBackup.Core.Exceptions;
using LogBackup.Core.Models;
using LogBackup.Core.Restoration;

namespace LogBackup.Tests;

public class BackupRestoreTests
{
    [Fact]
    public async Task FullBackupThenRestoreProducesIdenticalFiles()
    {
        using var h = new TestHarness();
        h.WriteSourceFile("app.log", "line1\nline2\n");
        h.WriteSourceFile("nested/error.log", "boom");

        var source = new BackupSourceConfig { Path = h.SourceDir, Recursive = true };
        var backupResult = await h.BackupEngine.CreateBackupAsync(source, h.KeyId, BackupMode.Full);

        Assert.Equal(BackupStatus.Verified, backupResult.Metadata.Status);
        Assert.Equal(2, backupResult.Metadata.FileCount);
        Assert.Empty(backupResult.SkippedFiles);

        var restoreResult = await h.RestoreEngine.RestoreAsync(new RestoreOptions { BackupId = backupResult.Metadata.BackupId });

        Assert.True(restoreResult.IntegrityPassed);
        Assert.Equal(2, restoreResult.FilesRestored);
        Assert.Equal("line1\nline2\n", File.ReadAllText(Path.Combine(restoreResult.RestoreDirectory, "app.log")));
        Assert.Equal("boom", File.ReadAllText(Path.Combine(restoreResult.RestoreDirectory, "nested", "error.log")));
    }

    [Fact]
    public async Task IncrementalBackupOnlyIncludesChangedFiles()
    {
        using var h = new TestHarness();
        h.WriteSourceFile("a.log", "v1");
        h.WriteSourceFile("b.log", "v1");
        var source = new BackupSourceConfig { Path = h.SourceDir, Recursive = true };

        var full = await h.BackupEngine.CreateBackupAsync(source, h.KeyId, BackupMode.Full);
        Assert.Equal(2, full.Metadata.FileCount);

        await Task.Delay(20); // ensure mtime resolution difference
        h.WriteSourceFile("a.log", "v2 - changed");

        var incremental = await h.BackupEngine.CreateBackupAsync(source, h.KeyId, BackupMode.Incremental);

        Assert.Equal(1, incremental.Metadata.FileCount);
        Assert.Equal("a.log", incremental.Metadata.Files[0].RelativePath);
        Assert.Equal(full.Metadata.BackupId, incremental.Metadata.PreviousBackupId);
    }

    [Fact]
    public async Task TamperedArtifactBlocksRestoreUnlessForced()
    {
        using var h = new TestHarness();
        h.WriteSourceFile("app.log", "original content");
        var source = new BackupSourceConfig { Path = h.SourceDir, Recursive = true };

        var backupResult = await h.BackupEngine.CreateBackupAsync(source, h.KeyId, BackupMode.Full);
        var artifactPath = h.Storage.GetAbsolutePath(backupResult.Metadata.ArtifactFileName);
        await File.AppendAllTextAsync(artifactPath, "tampered-bytes");

        var ex = await Assert.ThrowsAsync<IntegrityVerificationException>(() =>
            h.RestoreEngine.RestoreAsync(new RestoreOptions { BackupId = backupResult.Metadata.BackupId }));
        Assert.Contains("Integrity check failed", ex.Message);
    }

    [Fact]
    public async Task RestoreWithoutOverwriteRefusesToClobberExistingFile()
    {
        using var h = new TestHarness();
        h.WriteSourceFile("app.log", "content");
        var source = new BackupSourceConfig { Path = h.SourceDir, Recursive = true };
        var backupResult = await h.BackupEngine.CreateBackupAsync(source, h.KeyId, BackupMode.Full);

        var restoreDir = Path.Combine(h.RootDir, "custom-restore");
        Directory.CreateDirectory(restoreDir);
        File.WriteAllText(Path.Combine(restoreDir, "app.log"), "pre-existing");

        await Assert.ThrowsAsync<LogBackupException>(() =>
            h.RestoreEngine.RestoreAsync(new RestoreOptions
            {
                BackupId = backupResult.Metadata.BackupId,
                OutputDirectory = restoreDir,
                Overwrite = false,
            }));

        Assert.Equal("pre-existing", File.ReadAllText(Path.Combine(restoreDir, "app.log")));
    }

    [Fact]
    public async Task RestoringUnknownBackupIdThrowsNotFound()
    {
        using var h = new TestHarness();
        await Assert.ThrowsAsync<BackupNotFoundException>(() =>
            h.RestoreEngine.RestoreAsync(new RestoreOptions { BackupId = "does-not-exist" }));
    }

    [Fact]
    public async Task LockedFileIsSkippedNotCorruptedAndRecordedAsFailure()
    {
        using var h = new TestHarness();
        h.WriteSourceFile("ok.log", "fine");
        var lockedPath = h.WriteSourceFile("locked.log", "locked content");
        var source = new BackupSourceConfig { Path = h.SourceDir, Recursive = true };

        await using (new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = await h.BackupEngine.CreateBackupAsync(source, h.KeyId, BackupMode.Full);

            Assert.Equal(1, result.Metadata.FileCount);
            Assert.Single(result.SkippedFiles);
            Assert.Contains("locked.log", result.SkippedFiles[0]);
            Assert.Equal(BackupStatus.Verified, result.Metadata.Status);
        }
    }
}
