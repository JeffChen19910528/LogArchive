namespace LogBackup.Core.Abstractions;

/// <summary>
/// Abstracts the backing store for backup artifacts and metadata so local disk,
/// network shares, and object storage are interchangeable.
/// </summary>
public interface IStorageProvider
{
    Task WriteFileAsync(string relativePath, Stream content, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default);
    Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default);
    Task DeleteAsync(string relativePath, CancellationToken ct = default);
    Task MoveAsync(string sourceRelativePath, string destinationRelativePath, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListFilesAsync(string relativeDirectory, string searchPattern, CancellationToken ct = default);
    string GetAbsolutePath(string relativePath);
}
