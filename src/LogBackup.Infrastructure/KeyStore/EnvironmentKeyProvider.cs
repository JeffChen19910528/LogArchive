using LogBackup.Core.Abstractions;
using LogBackup.Core.Exceptions;

namespace LogBackup.Infrastructure.KeyStore;

/// <summary>
/// Development key provider: reads a base64-encoded 256-bit key from an environment variable
/// named LOGBACKUP_KEY_&lt;KEY_ID&gt; (key id upper-cased, non-alphanumerics replaced with '_').
/// Intended for local development only - see FileKeyProvider / OS secret stores for production.
/// </summary>
public sealed class EnvironmentKeyProvider : IKeyProvider
{
    public Task<byte[]> GetOrCreateKeyAsync(string keyId, CancellationToken ct = default)
    {
        var varName = ToEnvVarName(keyId);
        var value = Environment.GetEnvironmentVariable(varName);

        if (string.IsNullOrEmpty(value))
        {
            throw new KeyAccessException(
                $"Encryption key for key_id '{keyId}' was not found. Set environment variable {varName} " +
                "to a base64-encoded 256-bit key, e.g.: " +
                "$env:{0}=[Convert]::ToBase64String((1..32|%{{Get-Random -Max 256}}))".Replace("{0}", varName));
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(value);
        }
        catch (FormatException ex)
        {
            throw new KeyAccessException($"Environment variable {varName} does not contain valid base64.", ex);
        }

        if (key.Length != 32)
        {
            throw new KeyAccessException($"Key for '{keyId}' must be 256 bits (32 bytes) once base64-decoded; got {key.Length} bytes.");
        }

        return Task.FromResult(key);
    }

    private static string ToEnvVarName(string keyId)
    {
        var sanitized = new string(keyId.ToUpperInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return $"LOGBACKUP_KEY_{sanitized}";
    }
}
