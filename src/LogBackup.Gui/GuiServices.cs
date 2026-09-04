using LogBackup.Core.Abstractions;
using LogBackup.Core.Backup;
using LogBackup.Core.Encryption;
using LogBackup.Core.Hashing;
using LogBackup.Core.Models;
using LogBackup.Core.Restoration;
using LogBackup.Core.Retention;
using LogBackup.Infrastructure.Audit;
using LogBackup.Infrastructure.Configuration;
using LogBackup.Infrastructure.Database;
using LogBackup.Infrastructure.KeyStore;
using LogBackup.Infrastructure.Storage;

namespace LogBackup.Gui;

/// <summary>
/// The GUI's own composition root - wires up config + storage + crypto + metadata + audit for
/// a given config path, mirroring LogBackup.CLI's AppServices exactly. Kept as a separate copy
/// rather than a shared reference so the GUI project doesn't inherit the CLI project's
/// self-contained/single-file publish settings (RuntimeIdentifiers, PublishSingleFile, etc.),
/// which only make sense for that executable.
/// </summary>
public sealed class GuiServices
{
    public LogBackupConfig Config { get; }
    public IStorageProvider Storage { get; }
    public IMetadataStore MetadataStore { get; }
    public IAuditLogger Audit { get; }
    public IHashEngine Hash { get; }
    public BackupEngine BackupEngine { get; }
    public RestoreEngine RestoreEngine { get; }
    public RetentionEngine RetentionEngine { get; }
    public string ConfigPath { get; }

    private GuiServices(
        LogBackupConfig config,
        string configPath,
        IStorageProvider storage,
        IMetadataStore metadataStore,
        IAuditLogger audit,
        IHashEngine hash,
        BackupEngine backupEngine,
        RestoreEngine restoreEngine,
        RetentionEngine retentionEngine)
    {
        Config = config;
        ConfigPath = configPath;
        Storage = storage;
        MetadataStore = metadataStore;
        Audit = audit;
        Hash = hash;
        BackupEngine = backupEngine;
        RestoreEngine = restoreEngine;
        RetentionEngine = retentionEngine;
    }

    public static GuiServices Create(string? configPathOverride)
    {
        var configPath = configPathOverride ?? Path.Combine(".", "config", "logbackup.yaml");
        var config = ConfigLoader.Load(configPath);

        var storage = new LocalStorageProvider(config.Backup.Destination);
        var metadataStore = new SqliteMetadataStore(Path.Combine(config.Backup.Destination, "index", "backup.db"));
        var audit = new FileAuditLogger(config.Audit.Directory);
        var keyProvider = new CompositeKeyProvider(new EnvironmentKeyProvider(), new FileKeyProvider());
        var encryption = new AesGcmEncryptionEngine(keyProvider);
        var hash = new HashEngine(config.Backup.Hash.Algorithm);

        var backupEngine = new BackupEngine(encryption, hash, storage, metadataStore, audit);
        var restoreEngine = new RestoreEngine(encryption, hash, storage, metadataStore, audit, config.Restore.DefaultDirectory);
        var retentionEngine = new RetentionEngine(metadataStore, storage, audit);

        return new GuiServices(config, configPath, storage, metadataStore, audit, hash, backupEngine, restoreEngine, retentionEngine);
    }
}
