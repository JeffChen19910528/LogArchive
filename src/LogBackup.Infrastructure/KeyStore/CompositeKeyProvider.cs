using LogBackup.Core.Abstractions;
using LogBackup.Core.Exceptions;

namespace LogBackup.Infrastructure.KeyStore;

/// <summary>
/// Prefers an explicit environment-variable key (development / CI override) and falls back to
/// the OS-protected file-based key store (production default) when no env var is set.
/// </summary>
public sealed class CompositeKeyProvider : IKeyProvider
{
    private readonly EnvironmentKeyProvider _envProvider;
    private readonly FileKeyProvider _fileProvider;

    public CompositeKeyProvider(EnvironmentKeyProvider envProvider, FileKeyProvider fileProvider)
    {
        _envProvider = envProvider;
        _fileProvider = fileProvider;
    }

    public async Task<byte[]> GetOrCreateKeyAsync(string keyId, CancellationToken ct = default)
    {
        try
        {
            return await _envProvider.GetOrCreateKeyAsync(keyId, ct);
        }
        catch (KeyAccessException)
        {
            return await _fileProvider.GetOrCreateKeyAsync(keyId, ct);
        }
    }
}
