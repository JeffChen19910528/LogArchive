using System.CommandLine;
using LogBackup.CLI.Localization;

namespace LogBackup.CLI.Commands;

public static class AuditCommand
{
    public static Command Build(Option<string?> configOption, Option<string?> langOption)
    {
        var cmd = new Command("audit", Strings.T("audit.description"));
        var idOption = new Option<string?>("--id", Strings.T("audit.option.id"));
        cmd.AddOption(idOption);

        cmd.SetHandler(async (config, lang, id) =>
        {
            LanguageResolution.Apply(lang);
            Environment.ExitCode = await RunAsync(config, id);
        }, configOption, langOption, idOption);
        return cmd;
    }

    private static async Task<int> RunAsync(string? configPath, string? id)
    {
        var app = AppServices.Create(configPath);
        var records = await app.Audit.ReadAllAsync();

        if (id is not null)
        {
            records = records.Where(r => r.BackupId == id).ToList();
        }

        foreach (var r in records)
        {
            Console.WriteLine($"{r.TimestampUtc:O}  {r.Event,-28} backup={r.BackupId,-20} operator={r.Operator,-12} result={r.Result}{(r.Detail is null ? "" : $"  detail=\"{r.Detail}\"")}");
        }

        Console.WriteLine();
        Console.WriteLine(string.Format(Strings.T("audit.record_count"), records.Count));
        return ExitCode.Success;
    }
}
