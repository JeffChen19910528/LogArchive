using System.CommandLine;
using LogBackup.CLI.Localization;
using LogBackup.Core.Models;

namespace LogBackup.CLI.Commands;

public static class VerifyCommand
{
    public static Command Build(Option<string?> configOption, Option<string?> langOption)
    {
        var cmd = new Command("verify", Strings.T("verify.description"));
        var idOption = new Option<string>("--id", Strings.T("verify.option.id")) { IsRequired = true };
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
            Console.Error.WriteLine(string.Format(Strings.T("verify.error.not_found"), id));
            return ExitCode.BackupNotFound;
        }

        if (!await app.Storage.ExistsAsync(m.ArtifactFileName))
        {
            Console.Error.WriteLine(string.Format(Strings.T("verify.error.missing_artifact"), m.ArtifactFileName));
            m.Status = BackupStatus.Missing;
            await app.MetadataStore.SaveAsync(m);
            return ExitCode.FileNotFound;
        }

        string actual;
        await using (var stream = await app.Storage.OpenReadAsync(m.ArtifactFileName))
        {
            actual = await app.Hash.ComputeHashAsync(stream);
        }

        var pass = actual == m.Hash;

        Console.WriteLine(string.Format(Strings.T("verify.label.backup"), m.BackupId));
        Console.WriteLine(string.Format(Strings.T("verify.label.hash_algorithm"), m.HashAlgorithm));
        Console.WriteLine(string.Format(Strings.T("verify.label.expected"), m.Hash));
        Console.WriteLine(string.Format(Strings.T("verify.label.actual"), actual));
        Console.WriteLine(string.Format(Strings.T("verify.label.integrity"), pass ? Strings.T("verify.pass") : Strings.T("verify.fail")));

        m.Status = pass ? BackupStatus.Verified : BackupStatus.IntegrityFailed;
        await app.MetadataStore.SaveAsync(m);

        return pass ? ExitCode.Success : ExitCode.IntegrityVerificationFailed;
    }
}
