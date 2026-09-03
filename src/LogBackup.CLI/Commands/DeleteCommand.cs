using System.CommandLine;
using LogBackup.CLI.Localization;
using LogBackup.Core.Exceptions;

namespace LogBackup.CLI.Commands;

public static class DeleteCommand
{
    public static Command Build(Option<string?> configOption, Option<string?> langOption)
    {
        var cmd = new Command("delete", Strings.T("delete.description"));
        var idOption = new Option<string>("--id", Strings.T("delete.option.id")) { IsRequired = true };
        var yesOption = new Option<bool>("--yes", () => false, Strings.T("delete.option.yes"));
        var forceOption = new Option<bool>("--force", () => false, Strings.T("delete.option.force"));
        cmd.AddOption(idOption);
        cmd.AddOption(yesOption);
        cmd.AddOption(forceOption);

        cmd.SetHandler(async (config, lang, id, yes, force) =>
        {
            LanguageResolution.Apply(lang);
            Environment.ExitCode = await RunAsync(config, id, yes, force);
        }, configOption, langOption, idOption, yesOption, forceOption);
        return cmd;
    }

    private static async Task<int> RunAsync(string? configPath, string id, bool yes, bool force)
    {
        var app = AppServices.Create(configPath);

        if (!yes)
        {
            Console.WriteLine(Strings.T("delete.warning.title"));
            Console.WriteLine(Strings.T("delete.warning.body"));
            Console.WriteLine();
            Console.WriteLine(id);
            Console.WriteLine();
            Console.Write(Strings.T("delete.prompt.continue"));
            var answer = Console.ReadLine();
            if (!string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(Strings.T("delete.cancelled"));
                return ExitCode.Success;
            }
        }

        try
        {
            await app.RetentionEngine.DeleteAsync(id, force);
            Console.WriteLine(string.Format(Strings.T("delete.success"), id));
            return ExitCode.Success;
        }
        catch (BackupNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCode.BackupNotFound;
        }
        catch (BackupLockedException ex)
        {
            Console.Error.WriteLine(ex.Message + Strings.T("delete.error.locked"));
            return ExitCode.RetentionError;
        }
    }
}
