using LogBackup.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LogBackup.Infrastructure.Configuration;

public static class ConfigLoader
{
    public static LogBackupConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            return new LogBackupConfig();
        }

        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        return deserializer.Deserialize<LogBackupConfig>(yaml) ?? new LogBackupConfig();
    }

    public static void Save(LogBackupConfig config, string path)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, serializer.Serialize(config));
    }

    public static LogBackupConfig CreateDefault() => new()
    {
        Backup = new BackupConfig
        {
            Sources = new List<BackupSourceConfig>
            {
                new() { Path = "./logs", Recursive = true },
            },
            Destination = "./backup",
        },
    };
}
