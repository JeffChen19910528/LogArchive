using System.Formats.Tar;
using System.IO.Compression;
using LogBackup.Core.Abstractions;
using LogBackup.Core.Exceptions;
using LogBackup.Core.Models;

namespace LogBackup.Core.Restoration;

public sealed class RestoreOptions
{
    public required string BackupId { get; init; }
    public string? OutputDirectory { get; init; }
    public bool Overwrite { get; init; }
    public bool Force { get; init; }
}

public sealed class RestoreResult
{
    public required BackupMetadata Metadata { get; init; }
    public required string RestoreDirectory { get; init; }
    public required int FilesRestored { get; init; }
    public required bool IntegrityPassed { get; init; }
}

/// <summary>
/// Verifies the stored hash before ever attempting decryption. The hash is only ever used
/// as an integrity check - it is never used to reconstruct plaintext.
/// </summary>
public sealed class RestoreEngine
{
    private readonly IEncryptionEngine _encryptionEngine;
    private readonly IHashEngine _hashEngine;
    private readonly IStorageProvider _storage;
    private readonly IMetadataStore _metadataStore;
    private readonly IAuditLogger _audit;
    private readonly string _defaultRestoreDirectory;

    public RestoreEngine(
        IEncryptionEngine encryptionEngine,
        IHashEngine hashEngine,
        IStorageProvider storage,
        IMetadataStore metadataStore,
        IAuditLogger audit,
        string defaultRestoreDirectory)
    {
        _encryptionEngine = encryptionEngine;
        _hashEngine = hashEngine;
        _storage = storage;
        _metadataStore = metadataStore;
        _audit = audit;
        _defaultRestoreDirectory = defaultRestoreDirectory;
    }

    public async Task<RestoreResult> RestoreAsync(RestoreOptions options, CancellationToken ct = default)
    {
        var metadata = await _metadataStore.GetAsync(options.BackupId, ct)
            ?? throw new BackupNotFoundException(options.BackupId);

        await _audit.RecordAsync(new AuditRecord
        {
            Event = AuditEventType.RestoreStarted,
            BackupId = options.BackupId,
            Result = "in_progress",
        }, ct);

        try
        {
            if (!await _storage.ExistsAsync(metadata.ArtifactFileName, ct))
            {
                throw new LogBackupException($"Backup artifact is missing on disk: {metadata.ArtifactFileName}");
            }

            string actualHash;
            await using (var artifactStream = await _storage.OpenReadAsync(metadata.ArtifactFileName, ct))
            {
                actualHash = await _hashEngine.ComputeHashAsync(artifactStream, ct);
            }

            var integrityPassed = actualHash == metadata.Hash;

            await _audit.RecordAsync(new AuditRecord
            {
                Event = integrityPassed ? AuditEventType.HashVerificationSuccess : AuditEventType.HashVerificationFailed,
                BackupId = options.BackupId,
                Result = integrityPassed ? "success" : "failure",
            }, ct);

            if (!integrityPassed && !options.Force)
            {
                throw new IntegrityVerificationException(
                    $"Integrity check failed for backup {options.BackupId}. Expected {metadata.Hash}, got {actualHash}. Restore blocked. Use --force to override (audited).");
            }

            if (!integrityPassed && options.Force)
            {
                await _audit.RecordAsync(new AuditRecord
                {
                    Event = AuditEventType.RestoreForced,
                    BackupId = options.BackupId,
                    Result = "override",
                    Detail = $"Integrity check FAILED but restore was forced. Expected {metadata.Hash}, got {actualHash}.",
                }, ct);
            }

            var restoreRoot = options.OutputDirectory ?? Path.Combine(_defaultRestoreDirectory, options.BackupId);
            Directory.CreateDirectory(restoreRoot);

            byte[] decrypted;
            try
            {
                await using var artifactStream = await _storage.OpenReadAsync(metadata.ArtifactFileName, ct);
                using var plaintextMs = new MemoryStream();
                await _encryptionEngine.DecryptAsync(artifactStream, plaintextMs, metadata.KeyId, metadata.Nonce, ct);
                decrypted = plaintextMs.ToArray();
            }
            catch (Exception ex)
            {
                await _audit.RecordAsync(new AuditRecord
                {
                    Event = AuditEventType.RestoreFailed,
                    BackupId = options.BackupId,
                    Result = "failure",
                    Detail = $"Decryption failed: {ex.Message}",
                }, ct);
                throw new DecryptionFailedException($"Failed to decrypt backup {options.BackupId}. Wrong key or corrupted ciphertext.", ex);
            }

            using var gzipInput = new MemoryStream(decrypted);
            using var tarStream = new MemoryStream();
            await using (var gzip = new GZipStream(gzipInput, CompressionMode.Decompress))
            {
                await gzip.CopyToAsync(tarStream, ct);
            }
            tarStream.Position = 0;

            var filesRestored = 0;
            using var tarReader = new TarReader(tarStream);
            while (await tarReader.GetNextEntryAsync(cancellationToken: ct) is { } entry)
            {
                var destPath = ResolveSafeDestination(restoreRoot, entry.Name);

                if (!options.Overwrite && File.Exists(destPath))
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
                filesRestored++;
            }

            await _audit.RecordAsync(new AuditRecord
            {
                Event = AuditEventType.RestoreCompleted,
                BackupId = options.BackupId,
                Destination = restoreRoot,
                Result = "success",
            }, ct);

            return new RestoreResult
            {
                Metadata = metadata,
                RestoreDirectory = restoreRoot,
                FilesRestored = filesRestored,
                IntegrityPassed = integrityPassed,
            };
        }
        catch (Exception ex) when (ex is not LogBackupException)
        {
            await _audit.RecordAsync(new AuditRecord
            {
                Event = AuditEventType.RestoreFailed,
                BackupId = options.BackupId,
                Result = "failure",
                Detail = ex.Message,
            }, ct);
            throw new LogBackupException($"Restore failed: {ex.Message}", ex);
        }
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
