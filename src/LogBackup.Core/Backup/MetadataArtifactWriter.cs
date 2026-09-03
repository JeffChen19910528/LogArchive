using System.Text;
using System.Text.Json;
using LogBackup.Core.Abstractions;
using LogBackup.Core.Models;

namespace LogBackup.Core.Backup;

/// <summary>
/// Writes the human-readable sidecar files (metadata.json, .sha256) that sit next to a backup
/// artifact on disk, separately from the SQLite index that BackupEngine also updates.
/// </summary>
public static class MetadataArtifactWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task WriteAsync(IStorageProvider storage, BackupMetadata metadata, string datedDir, string artifactBaseName, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(metadata, JsonOptions);
        using var metaStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await storage.WriteFileAsync($"{datedDir}/{artifactBaseName}.metadata.json", metaStream, ct);

        using var hashStream = new MemoryStream(Encoding.UTF8.GetBytes(metadata.Hash));
        await storage.WriteFileAsync($"{datedDir}/{artifactBaseName}.sha256", hashStream, ct);
    }
}
