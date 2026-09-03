using LogBackup.Core.Models;

namespace LogBackup.Core.Abstractions;

public interface IAuditLogger
{
    Task RecordAsync(AuditRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<AuditRecord>> ReadAllAsync(CancellationToken ct = default);
}
