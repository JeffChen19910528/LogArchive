namespace LogBackup.Core.Models;

public enum BackupStatus
{
    Pending,
    Verified,
    IntegrityFailed,
    DecryptionFailed,
    Missing,
    Corrupted,
    Incomplete,
    Expired,
    Deleted,
}
