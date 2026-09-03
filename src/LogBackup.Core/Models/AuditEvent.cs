namespace LogBackup.Core.Models;

public static class AuditEventType
{
    public const string BackupStarted = "BACKUP_STARTED";
    public const string BackupCompleted = "BACKUP_COMPLETED";
    public const string BackupFailed = "BACKUP_FAILED";
    public const string HashGenerated = "HASH_GENERATED";
    public const string HashVerificationSuccess = "HASH_VERIFICATION_SUCCESS";
    public const string HashVerificationFailed = "HASH_VERIFICATION_FAILED";
    public const string RestoreStarted = "RESTORE_STARTED";
    public const string RestoreCompleted = "RESTORE_COMPLETED";
    public const string RestoreFailed = "RESTORE_FAILED";
    public const string BackupDeleted = "BACKUP_DELETED";
    public const string KeyAccessFailed = "KEY_ACCESS_FAILED";
    public const string AuthorizationFailed = "AUTHORIZATION_FAILED";
    public const string ConfigurationChanged = "CONFIGURATION_CHANGED";
    public const string RestoreForced = "RESTORE_FORCED_OVERRIDE";
}

public sealed class AuditRecord
{
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Event { get; set; } = string.Empty;
    public string? BackupId { get; set; }
    public string Operator { get; set; } = Environment.UserName;
    public string? Source { get; set; }
    public string? Destination { get; set; }
    public string Result { get; set; } = "success";
    public string? Detail { get; set; }
}
