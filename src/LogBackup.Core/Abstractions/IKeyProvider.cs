namespace LogBackup.Core.Abstractions;

/// <summary>
/// Resolves a symmetric encryption key by key_id. Keys are never stored in source control
/// or written into backup metadata - only the key_id is persisted.
/// </summary>
public interface IKeyProvider
{
    /// <summary>Returns a 32-byte (256-bit) key for the given key id, creating one on first use if supported.</summary>
    Task<byte[]> GetOrCreateKeyAsync(string keyId, CancellationToken ct = default);
}
