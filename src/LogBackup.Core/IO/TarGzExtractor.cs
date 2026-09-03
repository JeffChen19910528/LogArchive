using System.Formats.Tar;
using System.IO.Compression;
using LogBackup.Core.Exceptions;

namespace LogBackup.Core.IO;

/// <summary>
/// Extracts a gzip-compressed tar archive (as produced by <see cref="TarGzPacker"/>) to a
/// destination directory. Rejects any entry whose resolved path would land outside that
/// directory (zip-slip / path-traversal protection) - this is the only place that logic lives.
/// </summary>
public static class TarGzExtractor
{
    /// <returns>The number of files written.</returns>
    public static async Task<int> ExtractAsync(Stream gzipCompressedTar, string destinationRoot, bool overwrite, CancellationToken ct = default)
    {
        using var tarStream = new MemoryStream();
        await using (var gzip = new GZipStream(gzipCompressedTar, CompressionMode.Decompress))
        {
            await gzip.CopyToAsync(tarStream, ct);
        }
        tarStream.Position = 0;

        var filesWritten = 0;
        using var tarReader = new TarReader(tarStream);
        while (await tarReader.GetNextEntryAsync(cancellationToken: ct) is { } entry)
        {
            var destPath = ResolveSafeDestination(destinationRoot, entry.Name);

            if (!overwrite && File.Exists(destPath))
            {
                throw new LogBackupException(
                    $"Restore target already exists: {destPath}. Pass --overwrite to allow overwriting existing files.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            await using var outFile = new FileStream(destPath, FileMode.Create, FileAccess.Write);
            if (entry.DataStream is not null)
            {
                await entry.DataStream.CopyToAsync(outFile, ct);
            }
            filesWritten++;
        }

        return filesWritten;
    }

    /// <summary>Prevents path traversal / zip-slip style attacks from malicious archive entry names.</summary>
    private static string ResolveSafeDestination(string restoreRoot, string entryName)
    {
        var fullRoot = Path.GetFullPath(restoreRoot);
        var rootWithSeparator = fullRoot.EndsWith(Path.DirectorySeparatorChar) ? fullRoot : fullRoot + Path.DirectorySeparatorChar;
        var combined = Path.GetFullPath(Path.Combine(fullRoot, entryName));

        if (!combined.StartsWith(rootWithSeparator, StringComparison.Ordinal) && combined != fullRoot)
        {
            throw new LogBackupException($"Refusing to restore entry with unsafe path: {entryName}");
        }

        return combined;
    }
}
