using System.Security.Cryptography;
using LogBackup.Core.Abstractions;
using LogBackup.Core.Encryption;

namespace LogBackup.Tests;

public class EncryptionTests
{
    private const string KeyId = "enc-test-key";

    private static AesGcmEncryptionEngine CreateEngine()
    {
        Environment.SetEnvironmentVariable("LOGBACKUP_KEY_ENC_TEST_KEY", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        return new AesGcmEncryptionEngine(new LogBackup.Infrastructure.KeyStore.EnvironmentKeyProvider());
    }

    [Fact]
    public async Task RoundTripsPlaintext()
    {
        var engine = CreateEngine();
        var plaintext = "the quick brown fox jumps over the lazy dog"u8.ToArray();

        using var cipherStream = new MemoryStream();
        var result = await engine.EncryptAsync(new MemoryStream(plaintext), cipherStream, KeyId);

        cipherStream.Position = 0;
        using var plainOut = new MemoryStream();
        await engine.DecryptAsync(cipherStream, plainOut, KeyId, result.NonceBase64);

        Assert.Equal(plaintext, plainOut.ToArray());
    }

    [Fact]
    public async Task CorruptedCiphertextFailsAuthentication()
    {
        var engine = CreateEngine();
        var plaintext = "sensitive log data"u8.ToArray();

        using var cipherStream = new MemoryStream();
        var result = await engine.EncryptAsync(new MemoryStream(plaintext), cipherStream, KeyId);

        var corrupted = cipherStream.ToArray();
        corrupted[0] ^= 0xFF;

        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(async () =>
        {
            using var plainOut = new MemoryStream();
            await engine.DecryptAsync(new MemoryStream(corrupted), plainOut, KeyId, result.NonceBase64);
        });
    }

    [Fact]
    public async Task WrongNonceFailsAuthentication()
    {
        var engine = CreateEngine();
        var plaintext = "sensitive log data"u8.ToArray();

        using var cipherStream = new MemoryStream();
        await engine.EncryptAsync(new MemoryStream(plaintext), cipherStream, KeyId);
        cipherStream.Position = 0;

        var wrongNonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12));

        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(async () =>
        {
            using var plainOut = new MemoryStream();
            await engine.DecryptAsync(cipherStream, plainOut, KeyId, wrongNonce);
        });
    }

    [Fact]
    public async Task EachEncryptionUsesAFreshNonce()
    {
        var engine = CreateEngine();
        using var out1 = new MemoryStream();
        using var out2 = new MemoryStream();

        var r1 = await engine.EncryptAsync(new MemoryStream("a"u8.ToArray()), out1, KeyId);
        var r2 = await engine.EncryptAsync(new MemoryStream("a"u8.ToArray()), out2, KeyId);

        Assert.NotEqual(r1.NonceBase64, r2.NonceBase64);
    }
}
