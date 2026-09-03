using System.CommandLine;
using LogBackup.CLI.Localization;

namespace LogBackup.CLI.Commands;

public static class RetentionCommand
{
    public static Command Build(Option<string?> configOption, Option<string?> langOption)
    {
        var cmd = new Command("retention", Strings.T("retention.description"));
        var dryRunOption = new Option<bool>("--dry-run", () => false, Strings.T("retention.option.dry_run"));
        cmd.AddOption(dryRunOption);

        cmd.SetHandler(async (config, lang, dryRun) =>
        {
            LanguageResolution.Apply(lang);
            Environment.ExitCode = await RunAsync(config, dryRun);
        }, configOption, langOption, dryRunOption);
        return cmd;
    }

    private static async Task<int> RunAsync(string? configPath, bool dryRun)
    {
        var app = AppServices.Create(configPath);
        var policy = app.Config.Retention;

        if (policy.KeepDays is null && policy.KeepCount is null)
        {
            Console.WriteLine(Strings.T("retention.error.no_policy"));
            return ExitCode.Success;
        }

        if (dryRun)
        {
            Console.WriteLine(Strings.T("retention.dry_run_notice"));
        }

        var report = await app.RetentionEngine.ApplyAsync(policy, dryRun);

        Console.WriteLine($"{(dryRun ? Strings.T("retention.would_delete") : Strings.T("retention.deleted"))}:  {report.Deleted.Count}");
        foreach (var id in report.Deleted) Console.WriteLine($"  - {id}");

        if (report.SkippedLocked.Count > 0)
        {
            Console.WriteLine(string.Format(Strings.T("retention.skipped_locked"), report.SkippedLocked.Count));
            foreach (var id in report.SkippedLocked) Console.WriteLine($"  - {id}");
        }

        Console.WriteLine(string.Format(Strings.T("retention.preserved"), report.Preserved.Count));

        return ExitCode.Success;
    }
}
