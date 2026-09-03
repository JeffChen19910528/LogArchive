using System.CommandLine;
using LogBackup.CLI.Localization;
using LogBackup.Infrastructure.Configuration;

namespace LogBackup.CLI.Commands;

public static class ConfigCommand
{
    public static Command Build(Option<string?> configOption, Option<string?> langOption)
    {
        var cmd = new Command("config", Strings.T("config.description"));

        var init = new Command("init", Strings.T("config.init.description"));
        init.SetHandler((config, lang) =>
        {
            LanguageResolution.Apply(lang);
            RunInit(config);
        }, configOption, langOption);

        var show = new Command("show", Strings.T("config.show.description"));
        show.SetHandler((config, lang) =>
        {
            LanguageResolution.Apply(lang);
            RunShow(config);
        }, configOption, langOption);

        cmd.AddCommand(init);
        cmd.AddCommand(show);
        return cmd;
    }

    private static void RunInit(string? configPath)
    {
        var path = configPath ?? Path.Combine(".", "config", "logbackup.yaml");
        if (File.Exists(path))
        {
            Console.WriteLine(string.Format(Strings.T("config.init.exists"), path));
            Environment.ExitCode = ExitCode.Success;
            return;
        }

        ConfigLoader.Save(ConfigLoader.CreateDefault(), path);
        Console.WriteLine(string.Format(Strings.T("config.init.written"), path));
        Environment.ExitCode = ExitCode.Success;
    }

    private static void RunShow(string? configPath)
    {
        var app = AppServices.Create(configPath);
        Console.WriteLine(string.Format(Strings.T("config.show.file"), app.ConfigPath));
        Console.WriteLine(string.Format(Strings.T("config.show.destination"), app.Config.Backup.Destination));
        Console.WriteLine(Strings.T("config.show.sources"));
        foreach (var s in app.Config.Backup.Sources)
        {
            Console.WriteLine(string.Format(Strings.T("config.show.source_item"), s.Path, s.Recursive));
        }
        Console.WriteLine(string.Format(Strings.T("config.show.compression"), app.Config.Backup.Compression.Algorithm, app.Config.Backup.Compression.Enabled));
        Console.WriteLine(string.Format(Strings.T("config.show.encryption"), app.Config.Backup.Encryption.Algorithm, app.Config.Backup.Encryption.KeyId));
        Console.WriteLine(string.Format(Strings.T("config.show.hash"), app.Config.Backup.Hash.Algorithm));
        Console.WriteLine(string.Format(Strings.T("config.show.retention"), app.Config.Retention.KeepDays, app.Config.Retention.KeepCount));
        Console.WriteLine(string.Format(Strings.T("config.show.restore_dir"), app.Config.Restore.DefaultDirectory));
        Console.WriteLine(string.Format(Strings.T("config.show.audit_dir"), app.Config.Audit.Directory, app.Config.Audit.Enabled));
        Console.WriteLine(string.Format(Strings.T("config.show.sanitization"), app.Config.Sanitization.Enabled));
        Console.WriteLine(string.Format(Strings.T("config.show.language"), app.Config.Ui.Language));
        Environment.ExitCode = ExitCode.Success;
    }
}
