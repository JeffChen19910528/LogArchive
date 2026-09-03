using System.CommandLine;
using LogBackup.CLI.Localization;
using LogBackup.Core.Exceptions;
using LogBackup.Core.Restoration;

namespace LogBackup.CLI.Commands;

public static class RestoreCommand
{
    public static Command Build(Option<string?> configOption, Option<string?> langOption)
    {
        var cmd = new Command("restore", Strings.T("restore.description"));
        var idOption = new Option<string>("--id", Strings.T("restore.option.id")) { IsRequired = true };
        var outputOption = new Option<string?>("--output", Strings.T("restore.option.output"));
        var overwriteOption = new Option<bool>("--overwrite", () => false, Strings.T("restore.option.overwrite"));
        var forceOption = new Option<bool>("--force", () => false, Strings.T("restore.option.force"));
        cmd.AddOption(idOption);
        cmd.AddOption(outputOption);
        cmd.AddOption(overwriteOption);
        cmd.AddOption(forceOption);

        cmd.SetHandler(async (config, lang, id, output, overwrite, force) =>
        {
            LanguageResolution.Apply(lang);
            Environment.ExitCode = await RunAsync(config, id, output, overwrite, force);
        }, configOption, langOption, idOption, outputOption, overwriteOption, forceOption);
        return cmd;
    }

    private static async Task<int> RunAsync(string? configPath, string id, string? output, bool overwrite, bool force)
    {
        var app = AppServices.Create(configPath);

        try
        {
            var result = await app.RestoreEngine.RestoreAsync(new RestoreOptions
            {
                BackupId = id,
                OutputDirectory = output,
                Overwrite = overwrite,
                Force = force,
            });

            Console.WriteLine(Strings.T("restore.completed"));
            Console.WriteLine();
            Console.WriteLine(string.Format(Strings.T("restore.label.id"), id));
            Console.WriteLine(string.Format(Strings.T("restore.label.integrity"),
                result.IntegrityPassed ? Strings.T("restore.integrity.pass") : Strings.T("restore.integrity.fail_forced")));
            Console.WriteLine(string.Format(Strings.T("restore.label.encryption"), result.Metadata.EncryptionAlgorithm));
            Console.WriteLine(string.Format(Strings.T("restore.label.files_restored"), result.FilesRestored));
            Console.WriteLine(string.Format(Strings.T("restore.label.directory"), result.RestoreDirectory));
            return ExitCode.Success;
        }
        catch (BackupNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCode.BackupNotFound;
        }
        catch (IntegrityVerificationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCode.IntegrityVerificationFailed;
        }
        catch (DecryptionFailedException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCode.DecryptionError;
        }
        catch (LogBackupException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCode.GeneralError;
        }
    }
}
