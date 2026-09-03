namespace LogBackup.Core.Models;

public sealed class LogBackupConfig
{
    public ApplicationConfig Application { get; set; } = new();
    public BackupConfig Backup { get; set; } = new();
    public RetentionConfig Retention { get; set; } = new();
    public RestoreConfig Restore { get; set; } = new();
    public AuditConfig Audit { get; set; } = new();
    public SanitizationConfig Sanitization { get; set; } = new();
    public UiConfig Ui { get; set; } = new();
}

public sealed class UiConfig
{
    /// <summary>CLI display language: "en" (default) or "zh-TW". Overridden by --lang / LOGBACKUP_LANG.</summary>
    public string Language { get; set; } = "en";
}

public sealed class ApplicationConfig
{
    public string Name { get; set; } = "LogBackup";
    public string Version { get; set; } = "1.0";
}

public sealed class BackupSourceConfig
{
    public string Path { get; set; } = string.Empty;
    public bool Recursive { get; set; } = true;
    public List<string> Include { get; set; } = new() { "*.log", "*.log.*" };
    public List<string> Exclude { get; set; } = new();
}

public sealed class BackupConfig
{
    public List<BackupSourceConfig> Sources { get; set; } = new();
    public string Destination { get; set; } = "./backup";
    public CompressionConfig Compression { get; set; } = new();
    public EncryptionConfig Encryption { get; set; } = new();
    public HashConfig Hash { get; set; } = new();
}

public sealed class CompressionConfig
{
    public bool Enabled { get; set; } = true;
    public string Algorithm { get; set; } = "gzip";
}

public sealed class EncryptionConfig
{
    public bool Enabled { get; set; } = true;
    public string Algorithm { get; set; } = "AES-256-GCM";
    public string KeyId { get; set; } = "default";
}

public sealed class HashConfig
{
    public string Algorithm { get; set; } = "SHA-256";
}

public sealed class RetentionConfig
{
    public int? KeepDays { get; set; }
    public int? KeepCount { get; set; }
}

public sealed class RestoreConfig
{
    public string DefaultDirectory { get; set; } = "./restore";
}

public sealed class AuditConfig
{
    public bool Enabled { get; set; } = true;
    public string Directory { get; set; } = "./audit";
}

public sealed class SanitizationConfig
{
    public bool Enabled { get; set; } = false;
    public List<string> Patterns { get; set; } = new();
}
