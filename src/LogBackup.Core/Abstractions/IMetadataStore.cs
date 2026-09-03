using LogBackup.Core.Models;

namespace LogBackup.Core.Abstractions;

/// <summary>Index of all known backups, used by list/verify/restore/retention. Backed by SQLite.</summary>
public interface IMetadataStore
{
    Task SaveAsync(BackupMetadata metadata, CancellationToken ct = default);
    Task<BackupMetadata?> GetAsync(string backupId, CancellationToken ct = default);
    Task<IReadOnlyList<BackupMetadata>> ListAsync(CancellationToken ct = default);
    Task DeleteAsync(string backupId, CancellationToken ct = default);
    Task SetLockedAsync(string backupId, bool locked, CancellationToken ct = default);
    Task<bool> IsLockedAsync(string backupId, CancellationToken ct = default);
}
