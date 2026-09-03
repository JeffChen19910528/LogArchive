using System.CommandLine;
using LogBackup.CLI.Localization;
using LogBackup.Core.Backup;
using LogBackup.Core.Exceptions;
using LogBackup.Core.Models;

namespace LogBackup.CLI.Commands;

public static class BackupCommand
{
    public static Command Build(Option<string?> configOption, Option<string?> langOption)
    {
        var cmd = new Command("backup", Strings.T("backup.description"));
        var sourceOption = new Option<string?>("--source", Strings.T("backup.option.source"));
        var incrementalOption = new Option<bool>("--incremental", () => false, Strings.T("backup.option.incremental"));
        cmd.AddOption(sourceOption);
        cmd.AddOption(incrementalOption);

        cmd.SetHandler(async (config, lang, source, incremental) =>
        {
            LanguageResolution.Apply(lang);
            Environment.ExitCode = await RunAsync(config, source, incremental);
        }, configOption, langOption, sourceOption, incrementalOption);

        return cmd;
    }

    private static async Task<int> RunAsync(string? configPath, string? sourceOverride, bool incremental)
    {
        var app = AppServices.Create(configPath);
        var sources = app.Config.Backup.Sources;

        if (sourceOverride is not null)
        {
            sources = new List<BackupSourceConfig> { new() { Path = sourceOverride, Recursive = true } };
        }

        if (sources.Count == 0)
        {
            Console.Error.WriteLine(Strings.T("backup.error.no_sources"));
            return ExitCode.InvalidArguments;
        }

        var mode = incremental ? BackupMode.Incremental : BackupMode.Full;
        var overallExit = ExitCode.Success;

        foreach (var source in sources)
        {
            try
            {
                var result = await app.BackupEngine.CreateBackupAsync(source, app.Config.Backup.Encryption.KeyId, mode);
                var m = result.Metadata;

                Console.WriteLine(string.Format(Strings.T("backup.label.id"), m.BackupId));
                Console.WriteLine(string.Format(Strings.T("backup.label.source"), m.Source));
                Console.WriteLine(string.Format(Strings.T("backup.label.mode"), m.BackupMode));
                Console.WriteLine(string.Format(Strings.T("backup.label.files_backed_up"), m.FileCount));
                if (result.SkippedFiles.Count > 0)
                {
                    Console.WriteLine(string.Format(Strings.T("backup.label.files_skipped"), result.SkippedFiles.Count));
                }
                Console.WriteLine(string.Format(Strings.T("backup.label.original_size"), m.OriginalSize));
                Console.WriteLine(string.Format(Strings.T("backup.label.encrypted_size"), m.EncryptedSize));
                Console.WriteLine(string.Format(Strings.T("backup.label.encryption"), m.EncryptionAlgorithm));
                Console.WriteLine(string.Format(Strings.T("backup.label.hash"), m.HashAlgorithm, m.Hash));
                Console.WriteLine(string.Format(Strings.T("backup.label.status"), m.Status));
                Console.WriteLine();

                if (m.Status != BackupStatus.Verified)
                {
                    overallExit = ExitCode.IntegrityVerificationFailed;
                }
            }
            catch (KeyAccessException ex)
            {
                Console.Error.WriteLine(string.Format(Strings.T("backup.error.key_access"), ex.Message));
                overallExit = ExitCode.EncryptionError;
            }
            catch (LogBackupException ex)
            {
                Console.Error.WriteLine(string.Format(Strings.T("backup.error.failed"), source.Path, ex.Message));
                overallExit = ExitCode.GeneralError;
            }
        }

        return overallExit;
    }
}
