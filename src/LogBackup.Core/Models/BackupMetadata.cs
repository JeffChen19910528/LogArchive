namespace LogBackup.Core.Models;

public sealed class BackupFileEntry
{
    public string RelativePath { get; set; } = string.Empty;
    public long OriginalSize { get; set; }
    public DateTimeOffset LastModifiedUtc { get; set; }
    public string SourceHash { get; set; } = string.Empty;
}

public sealed class BackupMetadata
{
    public string BackupId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string BackupMode { get; set; } = "full";
    public int FileCount { get; set; }
    public long OriginalSize { get; set; }
    public long CompressedSize { get; set; }
    public long EncryptedSize { get; set; }
    public string EncryptionAlgorithm { get; set; } = string.Empty;
    public string HashAlgorithm { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;
    public BackupStatus Status { get; set; } = BackupStatus.Pending;
    public string ArtifactFileName { get; set; } = string.Empty;
    public List<BackupFileEntry> Files { get; set; } = new();
    public string? PreviousBackupId { get; set; }
}
