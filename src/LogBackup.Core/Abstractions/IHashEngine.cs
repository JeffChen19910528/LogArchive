namespace LogBackup.Core.Abstractions;

/// <summary>
/// Cryptographic hashing for integrity verification only. A hash is one-way and MUST NEVER
/// be used to reconstruct plaintext or as an encryption mechanism.
/// </summary>
public interface IHashEngine
{
    string Algorithm { get; }

    Task<string> ComputeHashAsync(Stream content, CancellationToken ct = default);
}
