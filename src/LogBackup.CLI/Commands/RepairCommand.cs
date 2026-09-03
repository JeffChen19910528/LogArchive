using System.CommandLine;
using LogBackup.CLI.Localization;

namespace LogBackup.CLI.Commands;

/// <summary>
/// Crash recovery: finds leftover *.tmp artifacts from interrupted backups. These were never
/// renamed to their final name, so they were never indexed and are never treated as valid -
/// this command simply surfaces and optionally removes them.
/// </summary>
public static class RepairCommand
{
    public static Command Build(Option<string?> configOption, Option<string?> langOption)
    {
        var cmd = new Command("repair", Strings.T("repair.description"));
        var deleteOption = new Option<bool>("--delete", () => false, Strings.T("repair.option.delete"));
        cmd.AddOption(deleteOption);

        cmd.SetHandler(async (config, lang, delete) =>
        {
            LanguageResolution.Apply(lang);
            Environment.ExitCode = await RunAsync(config, delete);
        }, configOption, langOption, deleteOption);
        return cmd;
    }

    private static async Task<int> RunAsync(string? configPath, bool delete)
    {
        var app = AppServices.Create(configPath);
        var incomplete = await app.Storage.ListFilesAsync(".", "*.tmp");

        if (incomplete.Count == 0)
        {
            Console.WriteLine(Strings.T("repair.none_found"));
            return ExitCode.Success;
        }

        Console.WriteLine(string.Format(Strings.T("repair.found"), incomplete.Count));
        foreach (var f in incomplete)
        {
            Console.WriteLine($"  - {f}");
            if (delete)
            {
                await app.Storage.DeleteAsync(f);
            }
        }

        if (delete)
        {
            Console.WriteLine(Strings.T("repair.deleted"));
        }
        else
        {
            Console.WriteLine(Strings.T("repair.hint"));
        }

        return ExitCode.Success;
    }
}
