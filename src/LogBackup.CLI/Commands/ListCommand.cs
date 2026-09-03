using System.CommandLine;
using LogBackup.CLI.Localization;

namespace LogBackup.CLI.Commands;

public static class ListCommand
{
    public static Command Build(Option<string?> configOption, Option<string?> langOption)
    {
        var cmd = new Command("list", Strings.T("list.description"));
        cmd.SetHandler(async (config, lang) =>
        {
            LanguageResolution.Apply(lang);
            Environment.ExitCode = await RunAsync(config);
        }, configOption, langOption);
        return cmd;
    }

    private static async Task<int> RunAsync(string? configPath)
    {
        var app = AppServices.Create(configPath);
        var backups = await app.MetadataStore.ListAsync();

        if (backups.Count == 0)
        {
            Console.WriteLine(Strings.T("list.empty"));
            return ExitCode.Success;
        }

        Console.WriteLine($"{Strings.T("list.header.id"),-27} {Strings.T("list.header.date"),-20} {Strings.T("list.header.size"),-10} {Strings.T("list.header.status"),-16}");
        Console.WriteLine(new string('-', 75));
        foreach (var b in backups)
        {
            var sizeMb = b.EncryptedSize / 1024.0 / 1024.0;
            var dateStr = b.CreatedAtUtc.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
            Console.WriteLine($"{b.BackupId,-27} {dateStr,-20} {sizeMb,8:F1} MB {b.Status,-16}");
        }

        return ExitCode.Success;
    }
}
