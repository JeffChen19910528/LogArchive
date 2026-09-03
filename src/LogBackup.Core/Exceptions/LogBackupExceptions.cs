namespace LogBackup.Core.Exceptions;

public class LogBackupException : Exception
{
    public LogBackupException(string message) : base(message) { }
    public LogBackupException(string message, Exception inner) : base(message, inner) { }
}

public sealed class IntegrityVerificationException : LogBackupException
{
    public IntegrityVerificationException(string message) : base(message) { }
}

public sealed class DecryptionFailedException : LogBackupException
{
    public DecryptionFailedException(string message, Exception inner) : base(message, inner) { }
}

public sealed class BackupNotFoundException : LogBackupException
{
    public BackupNotFoundException(string backupId) : base($"Backup not found: {backupId}") { }
}

public sealed class BackupLockedException : LogBackupException
{
    public BackupLockedException(string backupId) : base($"Backup is locked and cannot be modified: {backupId}") { }
}

public sealed class KeyAccessException : LogBackupException
{
    public KeyAccessException(string message) : base(message) { }
    public KeyAccessException(string message, Exception inner) : base(message, inner) { }
}
