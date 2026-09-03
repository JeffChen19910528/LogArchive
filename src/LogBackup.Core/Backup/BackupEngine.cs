using System.Runtime.InteropServices;
using LogBackup.Core.Abstractions;
using LogBackup.Core.Exceptions;
using LogBackup.Core.IO;
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
/// Orchestrates one backup run: discover -> read -> pack (tar+gzip) -> encrypt -> hash ->
/// verify -> atomic rename -> persist metadata. The individual steps live in their own
/// collaborators (<see cref="LogFileDiscovery"/>, <see cref="SafeFileReader"/>,
/// <see cref="TarGzPacker"/>, <see cref="MetadataArtifactWriter"/>) so this class only owns
/// sequencing and the atomic-write/verify contract described in Skill.md §28.
/// </summary>
public sealed class BackupEngine
{
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
        BackupSourceConfig source,
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

            var previous = mode == BackupMode.Incremental
                ? await FindPreviousVerifiedBackupAsync(sourceRoot, ct)
                : null;

            var (packEntries, fileEntries, skipped, originalSize) =
                await ReadChangedFilesAsync(sourceRoot, source, previous, backupId, ct);

            var pack = await TarGzPacker.PackAsync(packEntries, ct);
            await using var _packStream = pack.GzipStream;

            var artifactName = $"backup_{backupId}.tar.gz.enc";
            var datedDir = $"{now:yyyy}/{now:MM}/{now:dd}";
            var tmpRelativePath = $"{datedDir}/{artifactName}.tmp";
            var finalRelativePath = $"{datedDir}/{artifactName}";

            var encResult = await WriteEncryptedArtifactAsync(pack.GzipStream, tmpRelativePath, keyId, ct);

            var (status, hash, encryptedSize) = await VerifyTempArtifactAsync(tmpRelativePath, backupId, ct);

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
                CompressedSize = pack.CompressedSize,
                EncryptedSize = encryptedSize,
                EncryptionAlgorithm = encResult.Algorithm,
                HashAlgorithm = _hashEngine.Algorithm,
                Hash = hash,
                KeyId = encResult.KeyId,
                Nonce = encResult.NonceBase64,
                Status = status,
                ArtifactFileName = finalRelativePath,
                Files = fileEntries,
                PreviousBackupId = previous?.BackupId,
            };

            await MetadataArtifactWriter.WriteAsync(_storage, metadata, datedDir, artifactName.Replace(".tar.gz.enc", ""), ct);
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

    private async Task<BackupMetadata?> FindPreviousVerifiedBackupAsync(string sourceRoot, CancellationToken ct)
    {
        var all = await _metadataStore.ListAsync(ct);
        return all
            .Where(m => m.Source == sourceRoot && m.Status == BackupStatus.Verified)
            .OrderByDescending(m => m.CreatedAtUtc)
            .FirstOrDefault();
    }

    private async Task<(List<TarGzPacker.Entry> PackEntries, List<BackupFileEntry> FileEntries, List<string> Skipped, long OriginalSize)>
        ReadChangedFilesAsync(string sourceRoot, BackupSourceConfig source, BackupMetadata? previous, string backupId, CancellationToken ct)
    {
        var discovered = LogFileDiscovery.DiscoverFiles(sourceRoot, source);

        var packEntries = new List<TarGzPacker.Entry>();
        var fileEntries = new List<BackupFileEntry>();
        var skipped = new List<string>();
        long originalSize = 0;

        foreach (var file in discovered)
        {
            var info = new FileInfo(file);
            var relative = Path.GetRelativePath(sourceRoot, file);

            if (previous is not null && IsUnchangedSince(previous, relative, info))
            {
                continue;
            }

            var bytes = await SafeFileReader.TryReadAsync(file, ct);
            if (bytes is null)
            {
                skipped.Add(relative);
                await _audit.RecordAsync(new AuditRecord
                {
                    Event = AuditEventType.BackupFailed,
                    BackupId = backupId,
                    Source = file,
                    Result = "failure",
                    Detail = "File locked, unreadable, or permission denied after retries.",
                }, ct);
                continue;
            }

            packEntries.Add(new TarGzPacker.Entry(relative, bytes, info.LastWriteTimeUtc));
            fileEntries.Add(new BackupFileEntry
            {
                RelativePath = relative,
                OriginalSize = info.Length,
                LastModifiedUtc = info.LastWriteTimeUtc,
                SourceHash = await _hashEngine.ComputeHashAsync(new MemoryStream(bytes), ct),
            });
            originalSize += info.Length;
        }

        return (packEntries, fileEntries, skipped, originalSize);
    }

    private static bool IsUnchangedSince(BackupMetadata previous, string relativePath, FileInfo info)
    {
        var prevEntry = previous.Files.FirstOrDefault(f => f.RelativePath == relativePath);
        return prevEntry is not null
            && prevEntry.OriginalSize == info.Length
            && prevEntry.LastModifiedUtc == info.LastWriteTimeUtc;
    }

    private async Task<EncryptionResult> WriteEncryptedArtifactAsync(Stream gzipStream, string tmpRelativePath, string keyId, CancellationToken ct)
    {
        using var tmpOutput = new MemoryStream();
        var encResult = await _encryptionEngine.EncryptAsync(gzipStream, tmpOutput, keyId, ct);
        tmpOutput.Position = 0;
        await _storage.WriteFileAsync(tmpRelativePath, tmpOutput, ct);
        return encResult;
    }

    /// <summary>
    /// Atomic-backup contract (Skill.md §28): hash the freshly-written temp artifact, then
    /// re-read and re-hash it before the caller is allowed to rename it into place. A backup
    /// is only ever marked Verified once this second read/hash actually matches the first.
    /// </summary>
    private async Task<(BackupStatus Status, string Hash, long EncryptedSize)> VerifyTempArtifactAsync(string tmpRelativePath, string backupId, CancellationToken ct)
    {
        string hash;
        long encryptedSize;
        await using (var readBack = await _storage.OpenReadAsync(tmpRelativePath, ct))
        {
            encryptedSize = readBack.Length;
            hash = await _hashEngine.ComputeHashAsync(readBack, ct);
        }
        await _audit.RecordAsync(new AuditRecord { Event = AuditEventType.HashGenerated, BackupId = backupId, Result = "success" }, ct);

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

        return (status, verifyHash, encryptedSize);
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
