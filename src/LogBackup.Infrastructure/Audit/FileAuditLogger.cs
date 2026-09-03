using System.Text.Json;
using LogBackup.Core.Abstractions;
using LogBackup.Core.Models;

namespace LogBackup.Infrastructure.Audit;

/// <summary>
/// Append-only JSON-lines audit log, one file per UTC day. Writes are serialized with a
/// process-local lock and the file is opened in append mode so records cannot be overwritten.
/// </summary>
public sealed class FileAuditLogger : IAuditLogger
{
    private readonly string _directory;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileAuditLogger(string directory)
    {
        _directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(_directory);
    }

    public async Task RecordAsync(AuditRecord record, CancellationToken ct = default)
    {
        var line = JsonSerializer.Serialize(record) + Environment.NewLine;
        var path = GetLogPathFor(record.TimestampUtc);

        await _lock.WaitAsync(ct);
        try
        {
            await File.AppendAllTextAsync(path, line, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<AuditRecord>> ReadAllAsync(CancellationToken ct = default)
    {
        var records = new List<AuditRecord>();
        if (!Directory.Exists(_directory))
        {
            return records;
        }

        foreach (var file in Directory.EnumerateFiles(_directory, "audit-*.jsonl").OrderBy(f => f))
        {
            foreach (var line in await File.ReadAllLinesAsync(file, ct))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var record = JsonSerializer.Deserialize<AuditRecord>(line);
                if (record is not null) records.Add(record);
            }
        }
        return records;
    }

    private string GetLogPathFor(DateTimeOffset timestamp) => Path.Combine(_directory, $"audit-{timestamp:yyyy-MM-dd}.jsonl");
}
