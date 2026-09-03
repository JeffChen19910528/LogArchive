using System.Formats.Tar;
using System.IO.Compression;

namespace LogBackup.Core.IO;

/// <summary>
/// Packs a set of in-memory files into a tar archive, gzip-compressed. The counterpart to
/// <see cref="TarGzExtractor"/> - kept as the single place that knows the on-disk archive
/// format, so BackupEngine and RestoreEngine don't each reimplement it.
/// </summary>
public static class TarGzPacker
{
    public sealed record Entry(string RelativePath, byte[] Content, DateTimeOffset ModifiedUtc);

    public sealed record PackResult(Stream GzipStream, long CompressedSize);

    /// <summary>Caller owns and must dispose the returned <see cref="PackResult.GzipStream"/>.</summary>
    public static async Task<PackResult> PackAsync(IEnumerable<Entry> entries, CancellationToken ct = default)
    {
        using var tarStream = new MemoryStream();
        using (var tarWriter = new TarWriter(tarStream, TarEntryFormat.Pax, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                var tarEntry = new PaxTarEntry(TarEntryType.RegularFile, entry.RelativePath.Replace('\\', '/'))
                {
                    DataStream = new MemoryStream(entry.Content),
                    ModificationTime = entry.ModifiedUtc,
                };
                await tarWriter.WriteEntryAsync(tarEntry, ct);
            }
        }
        tarStream.Position = 0;

        var gzipStream = new MemoryStream();
        await using (var gzip = new GZipStream(gzipStream, CompressionLevel.Optimal, leaveOpen: true))
        {
            await tarStream.CopyToAsync(gzip, ct);
        }
        gzipStream.Position = 0;

        return new PackResult(gzipStream, gzipStream.Length);
    }
}
