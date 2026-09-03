using LogBackup.Core.Backup;

namespace LogBackup.Tests;

public class SafeFileReaderTests
{
    [Fact]
    public async Task ReturnsFileContentsWhenReadable()
    {
        var path = Path.Combine(Path.GetTempPath(), "safe-read-" + Guid.NewGuid().ToString("N") + ".log");
        try
        {
            await File.WriteAllTextAsync(path, "readable content");

            var bytes = await SafeFileReader.TryReadAsync(path);

            Assert.NotNull(bytes);
            Assert.Equal("readable content", System.Text.Encoding.UTF8.GetString(bytes!));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReturnsNullForNonExistentFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N") + ".log");

        var bytes = await SafeFileReader.TryReadAsync(path);

        Assert.Null(bytes);
    }

    [Fact]
    public async Task ReturnsNullWhenFileIsExclusivelyLocked()
    {
        var path = Path.Combine(Path.GetTempPath(), "locked-" + Guid.NewGuid().ToString("N") + ".log");
        try
        {
            await File.WriteAllTextAsync(path, "locked content");

            await using var lockHandle = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            var bytes = await SafeFileReader.TryReadAsync(path);

            Assert.Null(bytes);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
