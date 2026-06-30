using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;
using FtpBackupUploadTool.Core.Remote;

namespace FtpBackupUploadTool.Core.Services;

public sealed record UploadRunResult(IReadOnlyList<OperationLogEntry> Logs);

public sealed class UploadService
{
    private readonly IRemoteFileClient _draft;
    private readonly string? _localRoot;

    public UploadService(IRemoteFileClient draft, string? localRoot = null)
    {
        _draft = draft;
        _localRoot = localRoot;
    }

    public async Task<UploadRunResult> RunAsync(IReadOnlyList<RelativePath> paths, CancellationToken cancellationToken)
    {
        return await RunAsync(paths, _localRoot, cancellationToken);
    }

    public async Task<UploadRunResult> RunAsync(
        IReadOnlyList<RelativePath> paths,
        string? localRoot,
        CancellationToken cancellationToken)
    {
        var logs = new List<OperationLogEntry>();

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var localPath = ToLocalPath(path, localRoot);
            if (!File.Exists(localPath))
            {
                logs.Add(new OperationLogEntry(DateTimeOffset.Now, OperationLogLevel.Error, "Upload", path, "本地文件不存在", localPath));
                continue;
            }

            var parent = GetParent(path);
            if (parent is not null && !await _draft.DirectoryExistsAsync(parent, cancellationToken))
            {
                logs.Add(new OperationLogEntry(DateTimeOffset.Now, OperationLogLevel.Error, "Upload", path, "起案服务器目标父文件夹不存在", parent.Value));
                continue;
            }

            var isOverwrite = await _draft.FileExistsAsync(path, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await using var source = File.OpenRead(localPath);
            cancellationToken.ThrowIfCancellationRequested();
            await _draft.UploadAsync(path, source, cancellationToken);

            var message = isOverwrite ? "覆盖上传完成" : "上传完成";
            logs.Add(new OperationLogEntry(DateTimeOffset.Now, OperationLogLevel.Normal, "Upload", path, message));
        }

        return new UploadRunResult(logs);
    }

    private string ToLocalPath(RelativePath path) => ToLocalPath(path, _localRoot);

    private static string ToLocalPath(RelativePath path, string? localRoot)
    {
        if (string.IsNullOrWhiteSpace(localRoot))
        {
            throw new InvalidOperationException("Local root path is empty.");
        }

        var root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(localRoot));
        var fullPath = Path.GetFullPath(Path.Combine(root, path.Value.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!fullPath.Equals(root, StringComparison.OrdinalIgnoreCase)
            && !fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("本地路径超出根目录。");
        }

        return fullPath;
    }

    private static RelativePath? GetParent(RelativePath path)
    {
        var index = path.Value.LastIndexOf('/');
        return index <= 0 ? null : RelativePath.Parse(path.Value[..index]);
    }
}
