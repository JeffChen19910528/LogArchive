namespace LogBackup.Core.Backup;

/// <summary>
/// Reads a file for backup without ever writing to it, retrying briefly against transient
/// locks (e.g. another process still writing the log). Returns null - rather than throwing -
/// when the file could not be read after retries, so the caller can skip it and keep going.
/// </summary>
public static class SafeFileReader
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(250);

    public static async Task<byte[]?> TryReadAsync(string path, CancellationToken ct = default)
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var ms = new MemoryStream();
                await fs.CopyToAsync(ms, ct);
                return ms.ToArray();
            }
            catch (IOException) when (attempt < MaxRetries)
            {
                await Task.Delay(RetryDelay, ct);
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }
        return null;
    }
}
