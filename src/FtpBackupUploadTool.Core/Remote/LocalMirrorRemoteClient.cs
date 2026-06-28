using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;

namespace FtpBackupUploadTool.Core.Remote;

public sealed class LocalMirrorRemoteClient : IRemoteFileClient
{
    private readonly string _root;

    public LocalMirrorRemoteClient(string root)
    {
        _root = root;
        Directory.CreateDirectory(_root);
    }

    public Task<IReadOnlyList<FileEntry>> ListRecursiveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var files = Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)
            .Select(file =>
            {
                var relative = Path.GetRelativePath(_root, file).Replace('\\', '/');
                var info = new FileInfo(file);
                return new FileEntry(RelativePath.Parse(relative), false, info.Length, info.LastWriteTimeUtc);
            })
            .ToArray();

        return Task.FromResult<IReadOnlyList<FileEntry>>(files);
    }

    public Task<bool> FileExistsAsync(RelativePath path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(ToFullPath(path)));
    }

    public Task<bool> DirectoryExistsAsync(RelativePath path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Directory.Exists(ToFullPath(path)));
    }

    public Task<FileEntry?> GetFileEntryAsync(RelativePath path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = ToFullPath(path);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<FileEntry?>(null);
        }

        var info = new FileInfo(fullPath);
        return Task.FromResult<FileEntry?>(new FileEntry(path, false, info.Length, info.LastWriteTimeUtc));
    }

    public async Task DownloadAsync(RelativePath path, Stream destination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var source = File.OpenRead(ToFullPath(path));
        await source.CopyToAsync(destination, cancellationToken);
    }

    public async Task UploadAsync(RelativePath path, Stream source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = ToFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? _root);
        await using var destination = File.Create(fullPath);
        await source.CopyToAsync(destination, cancellationToken);
    }

    public Task DeleteFileAsync(RelativePath path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        File.Delete(ToFullPath(path));
        return Task.CompletedTask;
    }

    private string ToFullPath(RelativePath path)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, path.Value.Replace('/', Path.DirectorySeparatorChar)));
        var rootPath = Path.GetFullPath(_root);
        var rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;

        if (!fullPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase)
            && !fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("路径超出根目录。");
        }

        return fullPath;
    }
}
