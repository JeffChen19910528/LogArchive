using LogBackup.Core.Models;

namespace LogBackup.Core.Backup;

/// <summary>Resolves which files under a source directory match its include/exclude glob patterns.</summary>
public static class LogFileDiscovery
{
    public static List<string> DiscoverFiles(string sourceRoot, BackupSourceConfig source)
    {
        var searchOption = source.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var includePatterns = source.Include.Count > 0 ? source.Include : new List<string> { "*" };
        var matched = new HashSet<string>();

        foreach (var pattern in includePatterns)
        {
            foreach (var file in Directory.EnumerateFiles(sourceRoot, pattern, searchOption))
            {
                matched.Add(file);
            }
        }

        if (source.Exclude.Count > 0)
        {
            foreach (var pattern in source.Exclude)
            {
                foreach (var file in Directory.EnumerateFiles(sourceRoot, pattern, searchOption))
                {
                    matched.Remove(file);
                }
            }
        }

        return matched.OrderBy(f => f, StringComparer.Ordinal).ToList();
    }
}
