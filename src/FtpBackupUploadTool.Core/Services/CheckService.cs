using FtpBackupUploadTool.Core.Logging;
using FtpBackupUploadTool.Core.Formatting;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;
using FtpBackupUploadTool.Core.Remote;

namespace FtpBackupUploadTool.Core.Services;

public sealed record CheckRunResult(IReadOnlyList<OperationLogEntry> Logs, IReadOnlyList<CheckLogRow> Rows);

public sealed class CheckService
{
    private const string Operation = "Check";

    private readonly IRemoteFileClient _draft;
    private readonly string? _localRootPath;
    private readonly IRemoteFileClient _production;

    public CheckService(IRemoteFileClient production, IRemoteFileClient draft, string? localRootPath = null)
    {
        _production = production;
        _draft = draft;
        _localRootPath = localRootPath;
    }

    public async Task<CheckRunResult> RunAsync(IReadOnlyList<RelativePath> paths, CancellationToken cancellationToken)
    {
        return await RunAsync(paths, _localRootPath, cancellationToken);
    }

    public async Task<CheckRunResult> RunAsync(
        IReadOnlyList<RelativePath> paths,
        string? localRootPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var uniquePaths = GetUniquePaths(paths);
        var pathListSet = new HashSet<string>(uniquePaths.Select(path => path.Value), StringComparer.OrdinalIgnoreCase);
        var logs = new List<OperationLogEntry>();
        var rows = new List<CheckLogRow>();

        foreach (var path in uniquePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var draftEntry = await _draft.GetFileEntryAsync(path, cancellationToken);
            if (draftEntry is not null && !draftEntry.IsDirectory)
            {
                var message = $"文件更新：起案服务器中已上传路径 {path.Value} 对应的文件，修改日期：{FormatLastModified(draftEntry.LastModified)}";
                logs.Add(new OperationLogEntry(
                    DateTimeOffset.Now,
                    OperationLogLevel.Normal,
                    Operation,
                    path,
                    message));
                rows.Add(new CheckLogRow(path, OperationLogLevel.Normal, draftEntry.LastModified, message));
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var existsInProduction = await _production.FileExistsAsync(path, cancellationToken);
            if (existsInProduction)
            {
                var message = $"新路径旧文件：路径 {path.Value} 本次没有更新，但是生产服务器有文件。";
                logs.Add(new OperationLogEntry(
                    DateTimeOffset.Now,
                    OperationLogLevel.Warning,
                    Operation,
                    path,
                    message));
                rows.Add(new CheckLogRow(path, OperationLogLevel.Warning, null, message));
            }
            else
            {
                var message = $"文件缺失：路径 {path.Value} 对应文件缺失，起案服务器和生产服务器均没有对应文件。";
                logs.Add(new OperationLogEntry(
                    DateTimeOffset.Now,
                    OperationLogLevel.Error,
                    Operation,
                    path,
                    message));
                rows.Add(new CheckLogRow(path, OperationLogLevel.Error, null, message));
            }
        }

        foreach (var localPath in GetLocalFilePaths(localRootPath, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (pathListSet.Contains(localPath.Value))
            {
                continue;
            }

            var message = $"路径缺失：本地文件 {localPath.Value} 没有写入路径清单。";
            logs.Add(new OperationLogEntry(
                DateTimeOffset.Now,
                OperationLogLevel.Error,
                Operation,
                localPath,
                message));
            rows.Add(new CheckLogRow(localPath, OperationLogLevel.Error, null, message));
        }

        return new CheckRunResult(logs, rows);
    }

    private static IEnumerable<RelativePath> GetLocalFilePaths(string? localRootPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(localRootPath))
        {
            yield break;
        }

        var root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(localRootPath));
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            yield return RelativePath.Parse(relative);
        }
    }

    private static string FormatLastModified(DateTimeOffset? lastModified)
    {
        var display = TimeDisplayFormatter.FormatBeijingTime(lastModified);
        return string.IsNullOrWhiteSpace(display) ? "未知" : display;
    }

    private static IReadOnlyList<RelativePath> GetUniquePaths(IReadOnlyList<RelativePath> paths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unique = new List<RelativePath>();

        foreach (var path in paths)
        {
            if (seen.Add(path.Value))
            {
                unique.Add(path);
            }
        }

        return unique;
    }
}
