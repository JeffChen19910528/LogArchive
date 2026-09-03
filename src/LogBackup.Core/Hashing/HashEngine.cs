using System.Security.Cryptography;
using LogBackup.Core.Abstractions;

namespace LogBackup.Core.Hashing;

/// <summary>Supports SHA-256 (default), SHA-512, and SHA3-256. Never MD5 or SHA-1.</summary>
public sealed class HashEngine : IHashEngine
{
    public string Algorithm { get; }

    public HashEngine(string algorithm = "SHA-256")
    {
        Algorithm = Normalize(algorithm);
    }

    private static string Normalize(string algorithm)
    {
        var normalized = algorithm.Trim().ToUpperInvariant();
        return normalized switch
        {
            "SHA-256" or "SHA256" => "SHA-256",
            "SHA-512" or "SHA512" => "SHA-512",
            "SHA-3-256" or "SHA3-256" or "SHA3256" => "SHA3-256",
            _ => throw new NotSupportedException(
                $"Hash algorithm '{algorithm}' is not supported. Use SHA-256, SHA-512, or SHA3-256. MD5/SHA-1 are forbidden."),
        };
    }

    public async Task<string> ComputeHashAsync(Stream content, CancellationToken ct = default)
    {
        using HashAlgorithm hasher = Algorithm switch
        {
            "SHA-256" => SHA256.Create(),
            "SHA-512" => SHA512.Create(),
            "SHA3-256" => SHA3_256.IsSupported ? SHA3_256.Create() : throw new NotSupportedException("SHA3-256 is not supported on this platform."),
            _ => throw new NotSupportedException(Algorithm),
        };

        var hashBytes = await hasher.ComputeHashAsync(content, ct);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
