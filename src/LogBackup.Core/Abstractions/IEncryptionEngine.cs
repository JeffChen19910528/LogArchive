namespace LogBackup.Core.Abstractions;

public sealed record EncryptionResult(string Algorithm, string KeyId, string NonceBase64);

/// <summary>
/// Authenticated encryption only (AES-256-GCM / ChaCha20-Poly1305). Never MD5/SHA-1/ECB/custom ciphers.
/// </summary>
public interface IEncryptionEngine
{
    string Algorithm { get; }

    Task<EncryptionResult> EncryptAsync(Stream plaintext, Stream ciphertextOutput, string keyId, CancellationToken ct = default);

    Task DecryptAsync(Stream ciphertext, Stream plaintextOutput, string keyId, string nonceBase64, CancellationToken ct = default);
}
