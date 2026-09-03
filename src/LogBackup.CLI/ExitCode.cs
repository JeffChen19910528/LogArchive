namespace LogBackup.CLI;

public static class ExitCode
{
    public const int Success = 0;
    public const int GeneralError = 1;
    public const int InvalidArguments = 2;
    public const int FileNotFound = 3;
    public const int PermissionDenied = 4;
    public const int EncryptionError = 5;
    public const int DecryptionError = 6;
    public const int IntegrityVerificationFailed = 7;
    public const int BackupNotFound = 8;
    public const int RetentionError = 9;
    public const int AuthorizationFailed = 10;
}
