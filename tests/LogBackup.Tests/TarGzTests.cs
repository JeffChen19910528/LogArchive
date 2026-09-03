using LogBackup.Core.Exceptions;
using LogBackup.Core.IO;

namespace LogBackup.Tests;

public class TarGzTests
{
    [Fact]
    public async Task PackThenExtractRoundTripsFileContentAndRelativePaths()
    {
        var entries = new[]
        {
            new TarGzPacker.Entry("app.log", "hello"u8.ToArray(), DateTimeOffset.UtcNow),
            new TarGzPacker.Entry("nested/error.log", "boom"u8.ToArray(), DateTimeOffset.UtcNow),
        };

        var pack = await TarGzPacker.PackAsync(entries);
        using var gzipStream = pack.GzipStream;

        var destDir = Path.Combine(Path.GetTempPath(), "targz-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var filesWritten = await TarGzExtractor.ExtractAsync(gzipStream, destDir, overwrite: false);

            Assert.Equal(2, filesWritten);
            Assert.Equal("hello", File.ReadAllText(Path.Combine(destDir, "app.log")));
            Assert.Equal("boom", File.ReadAllText(Path.Combine(destDir, "nested", "error.log")));
        }
        finally
        {
            if (Directory.Exists(destDir)) Directory.Delete(destDir, recursive: true);
        }
    }

    [Fact]
    public async Task ExtractRejectsPathTraversalEntries()
    {
        var entries = new[] { new TarGzPacker.Entry("../escape.log", "pwned"u8.ToArray(), DateTimeOffset.UtcNow) };
        var pack = await TarGzPacker.PackAsync(entries);
        using var gzipStream = pack.GzipStream;

        var destDir = Path.Combine(Path.GetTempPath(), "targz-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            await Assert.ThrowsAsync<LogBackupException>(() => TarGzExtractor.ExtractAsync(gzipStream, destDir, overwrite: false));
        }
        finally
        {
            if (Directory.Exists(destDir)) Directory.Delete(destDir, recursive: true);
        }
    }
}
