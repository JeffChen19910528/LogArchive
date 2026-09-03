using System.Formats.Tar;
using System.IO.Compression;
using LogBackup.Core.Exceptions;
using LogBackup.Core.Models;
using LogBackup.Core.Restoration;

namespace LogBackup.Tests;

public class RestoreSecurityTests
{
    [Fact]
    public async Task MaliciousArchiveEntryPathTraversalIsRejected()
    {
        using var h = new TestHarness();

        // Hand-craft a backup artifact whose tar entry tries to escape the restore directory.
        using var tarStream = new MemoryStream();
        using (var tarWriter = new TarWriter(tarStream, TarEntryFormat.Pax, leaveOpen: true))
        {
            var maliciousEntry = new PaxTarEntry(TarEntryType.RegularFile, "../../evil.log")
            {
                DataStream = new MemoryStream("pwned"u8.ToArray()),
            };
            await tarWriter.WriteEntryAsync(maliciousEntry);
        }
        tarStream.Position = 0;

        using var gzipStream = new MemoryStream();
        await using (var gzip = new GZipStream(gzipStream, CompressionLevel.Optimal, leaveOpen: true))
        {
            await tarStream.CopyToAsync(gzip);
        }
        gzipStream.Position = 0;

        using var cipherStream = new MemoryStream();
        var encResult = await h.Encryption.EncryptAsync(gzipStream, cipherStream, h.KeyId);
        cipherStream.Position = 0;

        const string backupId = "malicious-001";
        var artifactRelativePath = "2026/01/01/malicious.tar.gz.enc";
        await h.Storage.WriteFileAsync(artifactRelativePath, cipherStream);

        string hash;
        await using (var readBack = await h.Storage.OpenReadAsync(artifactRelativePath))
        {
            hash = await h.Hash.ComputeHashAsync(readBack);
        }

        await h.MetadataStore.SaveAsync(new BackupMetadata
        {
            BackupId = backupId,
            Source = h.SourceDir,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ArtifactFileName = artifactRelativePath,
            EncryptionAlgorithm = h.Encryption.Algorithm,
            HashAlgorithm = h.Hash.Algorithm,
            Hash = hash,
            KeyId = h.KeyId,
            Nonce = encResult.NonceBase64,
            Status = BackupStatus.Verified,
        });

        await Assert.ThrowsAsync<LogBackupException>(() =>
            h.RestoreEngine.RestoreAsync(new RestoreOptions { BackupId = backupId }));
    }
}
