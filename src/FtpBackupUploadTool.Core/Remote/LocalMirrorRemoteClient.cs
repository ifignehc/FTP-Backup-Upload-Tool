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

    public Task<IReadOnlyList<FileEntry>> ListDirectoryAsync(RelativePath? directory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullDirectory = ToDirectoryFullPath(directory);
        if (!Directory.Exists(fullDirectory))
        {
            return Task.FromResult<IReadOnlyList<FileEntry>>(Array.Empty<FileEntry>());
        }

        var directories = Directory.EnumerateDirectories(fullDirectory)
            .Select(path =>
            {
                var relative = Path.GetRelativePath(_root, path).Replace('\\', '/');
                return new FileEntry(RelativePath.Parse(relative), true, 0, Directory.GetLastWriteTimeUtc(path));
            });
        var files = Directory.EnumerateFiles(fullDirectory)
            .Select(file =>
            {
                var relative = Path.GetRelativePath(_root, file).Replace('\\', '/');
                var info = new FileInfo(file);
                return new FileEntry(RelativePath.Parse(relative), false, info.Length, info.LastWriteTimeUtc);
            });

        return Task.FromResult<IReadOnlyList<FileEntry>>(directories.Concat(files).ToArray());
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
        var directory = Path.GetDirectoryName(fullPath) ?? _root;
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var destination = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await source.CopyToAsync(destination, cancellationToken);
                await destination.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(fullPath))
            {
                File.Replace(tempPath, fullPath, null);
            }
            else
            {
                File.Move(tempPath, fullPath);
            }
        }
        catch
        {
            DeleteTempFileIfExists(tempPath);
            throw;
        }
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

    private string ToDirectoryFullPath(RelativePath? path)
    {
        if (path is null)
        {
            return Path.GetFullPath(_root);
        }

        return ToFullPath(path);
    }

    private static void DeleteTempFileIfExists(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch
        {
        }
    }
}
