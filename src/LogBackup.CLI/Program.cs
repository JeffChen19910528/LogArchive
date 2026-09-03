using System.CommandLine;
using LogBackup.CLI.Commands;
using LogBackup.CLI.Localization;
using LogBackup.Infrastructure.Configuration;

namespace LogBackup.CLI;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Windows consoles default to a legacy OEM/ANSI codepage, which garbles the
        // zh-TW output. Force UTF-8 explicitly; this is a no-op on platforms that are
        // already UTF-8 by default, and is harmless when output is redirected/piped.
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch (IOException)
        {
            // Output is redirected to something that doesn't support codepage changes
            // (e.g. certain CI log collectors) - fall back to the default encoding.
        }

        Strings.Current = ResolveStartupLanguage(args);

        var configOption = new Option<string?>(
            new[] { "--config", "-c" },
            Strings.T("option.config"));

        var langOption = new Option<string?>(
            new[] { "--lang", "-l" },
            Strings.T("option.lang"));

        var root = new RootCommand(Strings.T("root.description"));
        root.AddGlobalOption(configOption);
        root.AddGlobalOption(langOption);

        root.AddCommand(BackupCommand.Build(configOption, langOption));
        root.AddCommand(ListCommand.Build(configOption, langOption));
        root.AddCommand(InfoCommand.Build(configOption, langOption));
        root.AddCommand(VerifyCommand.Build(configOption, langOption));
        root.AddCommand(RestoreCommand.Build(configOption, langOption));
        root.AddCommand(DeleteCommand.Build(configOption, langOption));
        root.AddCommand(RetentionCommand.Build(configOption, langOption));
        root.AddCommand(ConfigCommand.Build(configOption, langOption));
        root.AddCommand(AuditCommand.Build(configOption, langOption));
        root.AddCommand(RepairCommand.Build(configOption, langOption));

        var parseResult = await root.InvokeAsync(args);
        // Command handlers report the tool's own exit codes (see ExitCode.cs) via
        // Environment.ExitCode; System.CommandLine's own return value only reflects
        // argument-parsing failures, so a non-zero parse result always wins.
        return parseResult != 0 ? parseResult : Environment.ExitCode;
    }

    /// <summary>
    /// Command descriptions and --help text are built before System.CommandLine parses
    /// anything, so the display language for those has to be resolved from the raw argv
    /// (--lang / -l), then LOGBACKUP_LANG, then the ui.language of whatever --config points
    /// at (or the default config path), falling back to English. Each command handler then
    /// re-resolves the language from its properly-parsed --lang option value, which is the
    /// authoritative source for everything printed at runtime.
    /// </summary>
    private static UiLanguage ResolveStartupLanguage(string[] args)
    {
        var cliLang = ExtractOptionValue(args, "--lang", "-l");
        var parsed = Strings.Parse(cliLang);
        if (parsed is { } fromCli) return fromCli;

        var envLang = Strings.Parse(Environment.GetEnvironmentVariable("LOGBACKUP_LANG"));
        if (envLang is { } fromEnv) return fromEnv;

        var configPath = ExtractOptionValue(args, "--config", "-c") ?? Path.Combine(".", "config", "logbackup.yaml");
        try
        {
            var config = ConfigLoader.Load(configPath);
            var fromConfig = Strings.Parse(config.Ui.Language);
            if (fromConfig is { } lang) return lang;
        }
        catch
        {
            // Malformed config at this point is reported properly once AppServices.Create runs
            // inside the actual command handler; startup language resolution just falls back.
        }

        return UiLanguage.En;
    }

    private static string? ExtractOptionValue(string[] args, string longName, string shortName)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if ((arg == longName || arg == shortName) && i + 1 < args.Length)
            {
                return args[i + 1];
            }

            var prefix = longName + "=";
            if (arg.StartsWith(prefix, StringComparison.Ordinal))
            {
                return arg[prefix.Length..];
            }
        }
        return null;
    }
}
