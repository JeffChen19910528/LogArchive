using System.Runtime.InteropServices;
using System.Security.Cryptography;
using LogBackup.Core.Abstractions;
using LogBackup.Core.Exceptions;

namespace LogBackup.Infrastructure.KeyStore;

/// <summary>
/// Production-style key provider. Keys are generated on first use and persisted under the
/// user's local application-data directory:
///   Windows: encrypted at rest with DPAPI (CurrentUser scope) - closest built-in equivalent
///            to Windows Credential Manager without extra native interop.
///   Linux/macOS: stored as a key file with owner-only permissions (0600). True Secret
///            Service / Keychain integration requires additional native bindings and is a
///            documented follow-up (see Skill.md section 8) rather than an MVP requirement.
/// Never stores the key in source control, in application logs, or in backup metadata -
/// only the key_id travels with a backup.
/// </summary>
public sealed class FileKeyProvider : IKeyProvider
{
    private readonly string _keyDirectory;

    public FileKeyProvider(string? keyDirectory = null)
    {
        _keyDirectory = keyDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LogBackup", "keys");
        Directory.CreateDirectory(_keyDirectory);
    }

    public async Task<byte[]> GetOrCreateKeyAsync(string keyId, CancellationToken ct = default)
    {
        var path = GetKeyFilePath(keyId);

        try
        {
            if (File.Exists(path))
            {
                var stored = await File.ReadAllBytesAsync(path, ct);
                return Unprotect(stored);
            }

            var key = RandomNumberGenerator.GetBytes(32);
            var protectedBytes = Protect(key);
            await File.WriteAllBytesAsync(path, protectedBytes, ct);
            TrySetOwnerOnlyPermissions(path);
            return key;
        }
        catch (Exception ex) when (ex is not KeyAccessException)
        {
            throw new KeyAccessException($"Failed to access encryption key '{keyId}' at {path}.", ex);
        }
    }

    private string GetKeyFilePath(string keyId)
    {
        var sanitized = new string(keyId.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());
        return Path.Combine(_keyDirectory, $"{sanitized}.key");
    }

    private static byte[] Protect(byte[] key)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ProtectedData.Protect(key, optionalEntropy: null, DataProtectionScope.CurrentUser);
        }
        return key;
    }

    private static byte[] Unprotect(byte[] stored)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ProtectedData.Unprotect(stored, optionalEntropy: null, DataProtectionScope.CurrentUser);
        }
        return stored;
    }

    private static void TrySetOwnerOnlyPermissions(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return; // DPAPI already binds the key to the current user account.
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (PlatformNotSupportedException)
        {
            // best-effort only
        }
    }
}
