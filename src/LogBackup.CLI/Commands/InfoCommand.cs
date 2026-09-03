using System.CommandLine;
using LogBackup.CLI.Localization;

namespace LogBackup.CLI.Commands;

public static class InfoCommand
{
    public static Command Build(Option<string?> configOption, Option<string?> langOption)
    {
        var cmd = new Command("info", Strings.T("info.description"));
        var idOption = new Option<string>("--id", Strings.T("info.option.id")) { IsRequired = true };
        cmd.AddOption(idOption);

        cmd.SetHandler(async (config, lang, id) =>
        {
            LanguageResolution.Apply(lang);
            Environment.ExitCode = await RunAsync(config, id);
        }, configOption, langOption, idOption);
        return cmd;
    }

    private static async Task<int> RunAsync(string? configPath, string id)
    {
        var app = AppServices.Create(configPath);
        var m = await app.MetadataStore.GetAsync(id);
        if (m is null)
        {
            Console.Error.WriteLine(string.Format(Strings.T("info.error.not_found"), id));
            return ExitCode.BackupNotFound;
        }

        Console.WriteLine(string.Format(Strings.T("info.label.id"), m.BackupId));
        Console.WriteLine(string.Format(Strings.T("info.label.source"), m.Source));
        Console.WriteLine(string.Format(Strings.T("info.label.created"), m.CreatedAtUtc));
        Console.WriteLine(string.Format(Strings.T("info.label.platform"), m.Platform));
        Console.WriteLine(string.Format(Strings.T("info.label.hostname"), m.Hostname));
        Console.WriteLine(string.Format(Strings.T("info.label.mode"), m.BackupMode));
        Console.WriteLine(string.Format(Strings.T("info.label.file_count"), m.FileCount));
        Console.WriteLine(string.Format(Strings.T("info.label.original_size"), m.OriginalSize));
        Console.WriteLine(string.Format(Strings.T("info.label.compressed_size"), m.CompressedSize));
        Console.WriteLine(string.Format(Strings.T("info.label.encrypted_size"), m.EncryptedSize));
        Console.WriteLine(string.Format(Strings.T("info.label.encryption"), m.EncryptionAlgorithm));
        Console.WriteLine(string.Format(Strings.T("info.label.key_id"), m.KeyId));
        Console.WriteLine(string.Format(Strings.T("info.label.hash_algorithm"), m.HashAlgorithm));
        Console.WriteLine(string.Format(Strings.T("info.label.hash"), m.Hash));
        Console.WriteLine(string.Format(Strings.T("info.label.status"), m.Status));
        Console.WriteLine(string.Format(Strings.T("info.label.artifact"), m.ArtifactFileName));
        if (m.PreviousBackupId is not null)
        {
            Console.WriteLine(string.Format(Strings.T("info.label.previous"), m.PreviousBackupId));
        }

        return ExitCode.Success;
    }
}
