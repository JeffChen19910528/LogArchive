using LogBackup.Core.Abstractions;
using LogBackup.Core.Backup;
using LogBackup.Core.Encryption;
using LogBackup.Core.Hashing;
using LogBackup.Core.Restoration;
using LogBackup.Core.Retention;
using LogBackup.Infrastructure.Audit;
using LogBackup.Infrastructure.Database;
using LogBackup.Infrastructure.KeyStore;
using LogBackup.Infrastructure.Storage;

namespace LogBackup.Tests;

/// <summary>Wires up a full stack rooted in a fresh temp directory, and cleans it up on dispose.</summary>
public sealed class TestHarness : IDisposable
{
    public string RootDir { get; }
    public string SourceDir { get; }
    public IStorageProvider Storage { get; }
    public IMetadataStore MetadataStore { get; }
    public IAuditLogger Audit { get; }
    public IEncryptionEngine Encryption { get; }
    public IHashEngine Hash { get; }
    public BackupEngine BackupEngine { get; }
    public RestoreEngine RestoreEngine { get; }
    public RetentionEngine RetentionEngine { get; }

    private const string TestKeyId = "test-key";

    public TestHarness()
    {
        RootDir = Path.Combine(Path.GetTempPath(), "logbackup-tests-" + Guid.NewGuid().ToString("N"));
        SourceDir = Path.Combine(RootDir, "logs");
        Directory.CreateDirectory(SourceDir);

        Storage = new LocalStorageProvider(Path.Combine(RootDir, "backup"));
        MetadataStore = new SqliteMetadataStore(Path.Combine(RootDir, "backup", "index", "backup.db"));
        Audit = new FileAuditLogger(Path.Combine(RootDir, "audit"));

        Environment.SetEnvironmentVariable($"LOGBACKUP_KEY_{TestKeyId.ToUpperInvariant().Replace('-', '_')}",
            Convert.ToBase64String(new byte[32]));
        Encryption = new AesGcmEncryptionEngine(new EnvironmentKeyProvider());
        Hash = new HashEngine("SHA-256");

        BackupEngine = new BackupEngine(Encryption, Hash, Storage, MetadataStore, Audit);
        RestoreEngine = new RestoreEngine(Encryption, Hash, Storage, MetadataStore, Audit, Path.Combine(RootDir, "restore"));
        RetentionEngine = new RetentionEngine(MetadataStore, Storage, Audit);
    }

    public string KeyId => TestKeyId;

    public string WriteSourceFile(string relativeName, string content)
    {
        var path = Path.Combine(SourceDir, relativeName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(RootDir, recursive: true); } catch { /* best effort cleanup */ }
    }
}
