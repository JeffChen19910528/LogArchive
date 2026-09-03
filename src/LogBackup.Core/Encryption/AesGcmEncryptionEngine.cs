using System.Security.Cryptography;
using LogBackup.Core.Abstractions;

namespace LogBackup.Core.Encryption;

/// <summary>
/// Authenticated encryption using AES-256-GCM. Ciphertext layout on disk is
/// [ciphertext bytes][16-byte auth tag]; the nonce is stored in backup metadata, never on the artifact itself.
/// A fresh random 96-bit nonce is generated per backup.
/// </summary>
public sealed class AesGcmEncryptionEngine : IEncryptionEngine
{
    public string Algorithm => "AES-256-GCM";

    private readonly IKeyProvider _keyProvider;

    public AesGcmEncryptionEngine(IKeyProvider keyProvider)
    {
        _keyProvider = keyProvider;
    }

    public async Task<EncryptionResult> EncryptAsync(Stream plaintext, Stream ciphertextOutput, string keyId, CancellationToken ct = default)
    {
        var key = await _keyProvider.GetOrCreateKeyAsync(keyId, ct);
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);

        using var ms = new MemoryStream();
        await plaintext.CopyToAsync(ms, ct);
        var plaintextBytes = ms.ToArray();

        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];

        using (var aesGcm = new AesGcm(key, AesGcm.TagByteSizes.MaxSize))
        {
            aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        }
        CryptographicOperations.ZeroMemory(key);

        await ciphertextOutput.WriteAsync(ciphertext, ct);
        await ciphertextOutput.WriteAsync(tag, ct);

        return new EncryptionResult(Algorithm, keyId, Convert.ToBase64String(nonce));
    }

    public async Task DecryptAsync(Stream ciphertext, Stream plaintextOutput, string keyId, string nonceBase64, CancellationToken ct = default)
    {
        var key = await _keyProvider.GetOrCreateKeyAsync(keyId, ct);
        var nonce = Convert.FromBase64String(nonceBase64);

        using var ms = new MemoryStream();
        await ciphertext.CopyToAsync(ms, ct);
        var combined = ms.ToArray();

        byte[] plaintextBytes;
        try
        {
            plaintextBytes = DecryptCore(key, nonce, combined);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        await plaintextOutput.WriteAsync(plaintextBytes, ct);
    }

    // Span<byte> locals cannot live across an await in C# 12, so the actual AesGcm call
    // is isolated in a synchronous helper.
    private static byte[] DecryptCore(byte[] key, byte[] nonce, byte[] combined)
    {
        var tagSize = AesGcm.TagByteSizes.MaxSize;
        if (combined.Length < tagSize)
        {
            throw new CryptographicException("Ciphertext is too short to contain an authentication tag.");
        }

        var cipherLen = combined.Length - tagSize;
        var cipherOnly = combined.AsSpan(0, cipherLen);
        var tag = combined.AsSpan(cipherLen, tagSize);
        var plaintextBytes = new byte[cipherLen];

        using var aesGcm = new AesGcm(key, tagSize);
        aesGcm.Decrypt(nonce, cipherOnly, tag, plaintextBytes);
        return plaintextBytes;
    }
}
