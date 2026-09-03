using LogBackup.Core.Abstractions;
using LogBackup.Core.Exceptions;
using LogBackup.Core.Models;

namespace LogBackup.Core.Retention;

public sealed class RetentionReport
{
    public required IReadOnlyList<string> Deleted { get; init; }
    public required IReadOnlyList<string> Preserved { get; init; }
    public required IReadOnlyList<string> SkippedLocked { get; init; }
}

public sealed class RetentionEngine
{
    private readonly IMetadataStore _metadataStore;
    private readonly IStorageProvider _storage;
    private readonly IAuditLogger _audit;

    public RetentionEngine(IMetadataStore metadataStore, IStorageProvider storage, IAuditLogger audit)
    {
        _metadataStore = metadataStore;
        _storage = storage;
        _audit = audit;
    }

    public async Task<RetentionReport> ApplyAsync(RetentionConfig config, bool dryRun = false, CancellationToken ct = default)
    {
        var all = (await _metadataStore.ListAsync(ct))
            .Where(m => m.Status != BackupStatus.Deleted)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToList();

        var toDelete = new HashSet<string>();

        if (config.KeepDays is { } days)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
            foreach (var m in all.Where(m => m.CreatedAtUtc < cutoff))
            {
                toDelete.Add(m.BackupId);
            }
        }

        if (config.KeepCount is { } count)
        {
            foreach (var m in all.Skip(count))
            {
                toDelete.Add(m.BackupId);
            }
        }

        var deleted = new List<string>();
        var skippedLocked = new List<string>();

        foreach (var backupId in toDelete)
        {
            if (await _metadataStore.IsLockedAsync(backupId, ct))
            {
                skippedLocked.Add(backupId);
                continue;
            }

            if (dryRun)
            {
                deleted.Add(backupId);
                continue;
            }

            var metadata = all.First(m => m.BackupId == backupId);
            if (await _storage.ExistsAsync(metadata.ArtifactFileName, ct))
            {
                await _storage.DeleteAsync(metadata.ArtifactFileName, ct);
            }

            await _metadataStore.DeleteAsync(backupId, ct);
            deleted.Add(backupId);

            await _audit.RecordAsync(new AuditRecord
            {
                Event = AuditEventType.BackupDeleted,
                BackupId = backupId,
                Result = "success",
                Detail = "Deleted by retention policy.",
            }, ct);
        }

        var preserved = all.Select(m => m.BackupId).Except(deleted).Except(skippedLocked).ToList();

        return new RetentionReport { Deleted = deleted, Preserved = preserved, SkippedLocked = skippedLocked };
    }

    public async Task DeleteAsync(string backupId, bool force, CancellationToken ct = default)
    {
        var metadata = await _metadataStore.GetAsync(backupId, ct) ?? throw new BackupNotFoundException(backupId);

        if (!force && await _metadataStore.IsLockedAsync(backupId, ct))
        {
            throw new BackupLockedException(backupId);
        }

        if (await _storage.ExistsAsync(metadata.ArtifactFileName, ct))
        {
            await _storage.DeleteAsync(metadata.ArtifactFileName, ct);
        }

        await _metadataStore.DeleteAsync(backupId, ct);

        await _audit.RecordAsync(new AuditRecord
        {
            Event = AuditEventType.BackupDeleted,
            BackupId = backupId,
            Result = "success",
            Detail = "Deleted by explicit user request.",
        }, ct);
    }
}
