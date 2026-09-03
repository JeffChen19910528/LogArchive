using LogBackup.Core.Abstractions;

namespace LogBackup.Infrastructure.Storage;

/// <summary>Local-disk implementation of IStorageProvider, rooted at a base directory.</summary>
public sealed class LocalStorageProvider : IStorageProvider
{
    private readonly string _rootDirectory;

    public LocalStorageProvider(string rootDirectory)
    {
        _rootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(_rootDirectory);
    }

    public string GetAbsolutePath(string relativePath) => Path.Combine(_rootDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public async Task WriteFileAsync(string relativePath, Stream content, CancellationToken ct = default)
    {
        var fullPath = GetAbsolutePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fs, ct);
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = GetAbsolutePath(relativePath);
        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default)
        => Task.FromResult(File.Exists(GetAbsolutePath(relativePath)));

    public Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = GetAbsolutePath(relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        return Task.CompletedTask;
    }

    public Task MoveAsync(string sourceRelativePath, string destinationRelativePath, CancellationToken ct = default)
    {
        var src = GetAbsolutePath(sourceRelativePath);
        var dst = GetAbsolutePath(destinationRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
        File.Move(src, dst, overwrite: true);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListFilesAsync(string relativeDirectory, string searchPattern, CancellationToken ct = default)
    {
        var dir = GetAbsolutePath(relativeDirectory);
        if (!Directory.Exists(dir))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        IReadOnlyList<string> files = Directory.EnumerateFiles(dir, searchPattern, SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(_rootDirectory, f).Replace(Path.DirectorySeparatorChar, '/'))
            .ToList();
        return Task.FromResult(files);
    }
}
