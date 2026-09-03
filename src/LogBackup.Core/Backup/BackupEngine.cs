using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using LogBackup.Core.Abstractions;
using LogBackup.Core.Exceptions;
using LogBackup.Core.Models;

namespace LogBackup.Core.Backup;

public enum BackupMode
{
    Full,
    Incremental,
}

public sealed class BackupResult
{
    public required BackupMetadata Metadata { get; init; }
    public required IReadOnlyList<string> SkippedFiles { get; init; }
}

/// <summary>
/// Discovers log files, safely reads them (retrying against transient locks), compresses,
/// encrypts, hashes, and atomically stores the resulting backup artifact.
/// </summary>
public sealed class BackupEngine
{
    private const int MaxReadRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(250);

    private readonly IEncryptionEngine _encryptionEngine;
    private readonly IHashEngine _hashEngine;
    private readonly IStorageProvider _storage;
    private readonly IMetadataStore _metadataStore;
    private readonly IAuditLogger _audit;

    public BackupEngine(
        IEncryptionEngine encryptionEngine,
        IHashEngine hashEngine,
        IStorageProvider storage,
        IMetadataStore metadataStore,
        IAuditLogger audit)
    {
        _encryptionEngine = encryptionEngine;
        _hashEngine = hashEngine;
        _storage = storage;
        _metadataStore = metadataStore;
        _audit = audit;
    }

    public async Task<BackupResult> CreateBackupAsync(
        Models.BackupSourceConfig source,
        string keyId,
        BackupMode mode,
        CancellationToken ct = default)
    {
        var backupId = GenerateBackupId();
        var now = DateTimeOffset.UtcNow;

        await _audit.RecordAsync(new AuditRecord
        {
            Event = AuditEventType.BackupStarted,
            BackupId = backupId,
            Source = source.Path,
            Result = "in_progress",
        }, ct);

        try
        {
            var sourceRoot = Path.GetFullPath(source.Path);
            if (!Directory.Exists(sourceRoot))
            {
                throw new LogBackupException($"Source directory does not exist: {sourceRoot}");
            }

            var discovered = DiscoverFiles(sourceRoot, source);

            BackupMetadata? previous = mode == BackupMode.Incremental
                ? (await _metadataStore.ListAsync(ct))
                    .Where(m => m.Source == sourceRoot && m.Status == BackupStatus.Verified)
                    .OrderByDescending(m => m.CreatedAtUtc)
                    .FirstOrDefault()
                : null;

            var skipped = new List<string>();
            var included = new List<(string full, string relative, FileInfo info)>();

            foreach (var file in discovered)
            {
                var info = new FileInfo(file);
                var relative = Path.GetRelativePath(sourceRoot, file);

                if (mode == BackupMode.Incremental && previous is not null)
                {
                    var prevEntry = previous.Files.FirstOrDefault(f => f.RelativePath == relative);
                    if (prevEntry is not null
                        && prevEntry.OriginalSize == info.Length
                        && prevEntry.LastModifiedUtc == info.LastWriteTimeUtc)
                    {
                        continue; // unchanged, skip
                    }
                }

                included.Add((file, relative, info));
            }

            // tar -> gzip -> encrypt, streaming through temp buffers.
            using var tarStream = new MemoryStream();
            var fileEntries = new List<BackupFileEntry>();
            long originalSize = 0;

            using (var tarWriter = new TarWriter(tarStream, TarEntryFormat.Pax, leaveOpen: true))
            {
                foreach (var (full, relative, info) in included)
                {
                    byte[]? bytes = await TryReadFileWithRetryAsync(full, ct);
                    if (bytes is null)
                    {
                        skipped.Add(relative);
                        await _audit.RecordAsync(new AuditRecord
                        {
                            Event = AuditEventType.BackupFailed,
                            BackupId = backupId,
                            Source = full,
                            Result = "failure",
                            Detail = "File locked, unreadable, or permission denied after retries.",
                        }, ct);
                        continue;
                    }

                    var entry = new PaxTarEntry(TarEntryType.RegularFile, relative.Replace('\\', '/'))
                    {
                        DataStream = new MemoryStream(bytes),
                        ModificationTime = info.LastWriteTimeUtc,
                    };
                    await tarWriter.WriteEntryAsync(entry, ct);

                    fileEntries.Add(new BackupFileEntry
                    {
                        RelativePath = relative,
                        OriginalSize = info.Length,
                        LastModifiedUtc = info.LastWriteTimeUtc,
                        SourceHash = await _hashEngine.ComputeHashAsync(new MemoryStream(bytes), ct),
                    });
                    originalSize += info.Length;
                }
            }

            tarStream.Position = 0;
            using var gzipStream = new MemoryStream();
            await using (var gzip = new GZipStream(gzipStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                await tarStream.CopyToAsync(gzip, ct);
            }
            gzipStream.Position = 0;
            var compressedSize = gzipStream.Length;

            var artifactName = $"backup_{backupId}.tar.gz.enc";
            var datedDir = $"{now:yyyy}/{now:MM}/{now:dd}";
            var tmpRelativePath = $"{datedDir}/{artifactName}.tmp";
            var finalRelativePath = $"{datedDir}/{artifactName}";

            EncryptionResult encResult;
            using (var tmpOutput = new MemoryStream())
            {
                encResult = await _encryptionEngine.EncryptAsync(gzipStream, tmpOutput, keyId, ct);
                tmpOutput.Position = 0;
                await _storage.WriteFileAsync(tmpRelativePath, tmpOutput, ct);
            }

            // Hash the exact stored (still-temporary) artifact.
            string hash;
            long encryptedSize;
            await using (var readBack = await _storage.OpenReadAsync(tmpRelativePath, ct))
            {
                encryptedSize = readBack.Length;
                hash = await _hashEngine.ComputeHashAsync(readBack, ct);
            }
            await _audit.RecordAsync(new AuditRecord { Event = AuditEventType.HashGenerated, BackupId = backupId, Result = "success" }, ct);

            // Verify BEFORE renaming: re-read the temp artifact and recompute the hash.
            string verifyHash;
            await using (var verifyStream = await _storage.OpenReadAsync(tmpRelativePath, ct))
            {
                verifyHash = await _hashEngine.ComputeHashAsync(verifyStream, ct);
            }

            var status = hash == verifyHash ? BackupStatus.Verified : BackupStatus.IntegrityFailed;

            await _audit.RecordAsync(new AuditRecord
            {
                Event = status == BackupStatus.Verified ? AuditEventType.HashVerificationSuccess : AuditEventType.HashVerificationFailed,
                BackupId = backupId,
                Result = status == BackupStatus.Verified ? "success" : "failure",
            }, ct);

            // Only rename .tmp -> final artifact name once verification has actually succeeded.
            if (status == BackupStatus.Verified)
            {
                await _storage.MoveAsync(tmpRelativePath, finalRelativePath, ct);
            }
            else
            {
                finalRelativePath = tmpRelativePath; // leave as .tmp so it is never treated as a valid backup
            }

            var metadata = new BackupMetadata
            {
                BackupId = backupId,
                Source = sourceRoot,
                CreatedAtUtc = now,
                Platform = GetPlatformName(),
                Hostname = Environment.MachineName,
                BackupMode = mode.ToString().ToLowerInvariant(),
                FileCount = fileEntries.Count,
                OriginalSize = originalSize,
                CompressedSize = compressedSize,
                EncryptedSize = encryptedSize,
                EncryptionAlgorithm = encResult.Algorithm,
                HashAlgorithm = _hashEngine.Algorithm,
                Hash = verifyHash,
                KeyId = encResult.KeyId,
                Nonce = encResult.NonceBase64,
                Status = status,
                ArtifactFileName = finalRelativePath,
                Files = fileEntries,
                PreviousBackupId = previous?.BackupId,
            };

            await WriteMetadataArtifactsAsync(metadata, datedDir, artifactName, ct);
            await _metadataStore.SaveAsync(metadata, ct);

            await _audit.RecordAsync(new AuditRecord
            {
                Event = status == BackupStatus.Verified ? AuditEventType.BackupCompleted : AuditEventType.BackupFailed,
                BackupId = backupId,
                Source = sourceRoot,
                Destination = finalRelativePath,
                Result = status == BackupStatus.Verified ? "success" : "failure",
            }, ct);

            return new BackupResult { Metadata = metadata, SkippedFiles = skipped };
        }
        catch (Exception ex) when (ex is not LogBackupException)
        {
            await _audit.RecordAsync(new AuditRecord
            {
                Event = AuditEventType.BackupFailed,
                BackupId = backupId,
                Source = source.Path,
                Result = "failure",
                Detail = ex.Message,
            }, ct);
            throw new LogBackupException($"Backup failed: {ex.Message}", ex);
        }
    }

    private async Task WriteMetadataArtifactsAsync(BackupMetadata metadata, string datedDir, string artifactName, CancellationToken ct)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        });

        using var metaStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        await _storage.WriteFileAsync($"{datedDir}/{artifactName.Replace(".tar.gz.enc", "")}.metadata.json", metaStream, ct);

        using var hashStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(metadata.Hash));
        await _storage.WriteFileAsync($"{datedDir}/{artifactName.Replace(".tar.gz.enc", "")}.sha256", hashStream, ct);
    }

    private static async Task<byte[]?> TryReadFileWithRetryAsync(string path, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxReadRetries; attempt++)
        {
            try
            {
                await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var ms = new MemoryStream();
                await fs.CopyToAsync(ms, ct);
                return ms.ToArray();
            }
            catch (IOException) when (attempt < MaxReadRetries)
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

    private static List<string> DiscoverFiles(string sourceRoot, Models.BackupSourceConfig source)
    {
        var searchOption = source.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var includePatterns = source.Include.Count > 0 ? source.Include : new List<string> { "*" };
        var matched = new HashSet<string>();

        foreach (var pattern in includePatterns)
        {
            foreach (var file in Directory.EnumerateFiles(sourceRoot, pattern, searchOption))
            {
                matched.Add(file);
            }
        }

        if (source.Exclude.Count > 0)
        {
            foreach (var pattern in source.Exclude)
            {
                foreach (var file in Directory.EnumerateFiles(sourceRoot, pattern, searchOption))
                {
                    matched.Remove(file);
                }
            }
        }

        return matched.OrderBy(f => f, StringComparer.Ordinal).ToList();
    }

    private static string GenerateBackupId()
    {
        var now = DateTimeOffset.UtcNow;
        var suffix = Random.Shared.Next(0, 999).ToString("D3");
        return $"{now:yyyyMMdd-HHmmss}-{suffix}";
    }

    private static string GetPlatformName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "linux";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "macos";
        return RuntimeInformation.OSDescription;
    }
}
