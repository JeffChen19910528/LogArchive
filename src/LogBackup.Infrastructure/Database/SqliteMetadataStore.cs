using System.Text.Json;
using LogBackup.Core.Abstractions;
using LogBackup.Core.Models;
using Microsoft.Data.Sqlite;

namespace LogBackup.Infrastructure.Database;

/// <summary>SQLite-backed index of backup metadata (backup.db), used by list/verify/restore/retention.</summary>
public sealed class SqliteMetadataStore : IMetadataStore
{
    private readonly string _connectionString;

    public SqliteMetadataStore(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
        _connectionString = $"Data Source={databasePath}";
        Initialize();
    }

    private void Initialize()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS backups (
                backup_id TEXT PRIMARY KEY,
                created_at_utc TEXT NOT NULL,
                status TEXT NOT NULL,
                locked INTEGER NOT NULL DEFAULT 0,
                json TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public async Task SaveAsync(BackupMetadata metadata, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO backups (backup_id, created_at_utc, status, locked, json)
            VALUES ($id, $created, $status, 0, $json)
            ON CONFLICT(backup_id) DO UPDATE SET
                created_at_utc = excluded.created_at_utc,
                status = excluded.status,
                json = excluded.json;
            """;
        cmd.Parameters.AddWithValue("$id", metadata.BackupId);
        cmd.Parameters.AddWithValue("$created", metadata.CreatedAtUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$status", metadata.Status.ToString());
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(metadata));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<BackupMetadata?> GetAsync(string backupId, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT json FROM backups WHERE backup_id = $id;";
        cmd.Parameters.AddWithValue("$id", backupId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json ? JsonSerializer.Deserialize<BackupMetadata>(json) : null;
    }

    public async Task<IReadOnlyList<BackupMetadata>> ListAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT json FROM backups ORDER BY created_at_utc DESC;";
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var list = new List<BackupMetadata>();
        while (await reader.ReadAsync(ct))
        {
            var json = reader.GetString(0);
            var metadata = JsonSerializer.Deserialize<BackupMetadata>(json);
            if (metadata is not null) list.Add(metadata);
        }
        return list;
    }

    public async Task DeleteAsync(string backupId, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM backups WHERE backup_id = $id;";
        cmd.Parameters.AddWithValue("$id", backupId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SetLockedAsync(string backupId, bool locked, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE backups SET locked = $locked WHERE backup_id = $id;";
        cmd.Parameters.AddWithValue("$id", backupId);
        cmd.Parameters.AddWithValue("$locked", locked ? 1 : 0);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> IsLockedAsync(string backupId, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT locked FROM backups WHERE backup_id = $id;";
        cmd.Parameters.AddWithValue("$id", backupId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is long l && l == 1;
    }
}
