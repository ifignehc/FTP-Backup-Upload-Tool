using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;
using FtpBackupUploadTool.Core.Remote;

namespace FtpBackupUploadTool.Core.Services;

public sealed record CheckRunResult(IReadOnlyList<OperationLogEntry> Logs);

public sealed class CheckService
{
    private const string Operation = "Check";

    private readonly IRemoteFileClient _draft;
    private readonly IRemoteFileClient _production;

    public CheckService(IRemoteFileClient production, IRemoteFileClient draft)
    {
        _production = production;
        _draft = draft;
    }

    public async Task<CheckRunResult> RunAsync(IReadOnlyList<RelativePath> paths, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var draftEntries = await _draft.ListRecursiveAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var uniquePaths = GetUniquePaths(paths);
        var draftFilePaths = new HashSet<string>(
            draftEntries.Where(entry => !entry.IsDirectory).Select(entry => entry.Path.Value),
            StringComparer.OrdinalIgnoreCase);

        var logs = new List<OperationLogEntry>();

        foreach (var path in uniquePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (draftFilePaths.Contains(path.Value))
            {
                logs.Add(new OperationLogEntry(
                    DateTimeOffset.Now,
                    OperationLogLevel.Normal,
                    Operation,
                    path,
                    "文件更新：起案服务器存在该路径对应文件。"));
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var existsInProduction = await _production.FileExistsAsync(path, cancellationToken);
            if (existsInProduction)
            {
                logs.Add(new OperationLogEntry(
                    DateTimeOffset.Now,
                    OperationLogLevel.Warning,
                    Operation,
                    path,
                    "新路径、旧文件：起案服务器没有该文件，生产服务器仍存在旧文件。"));
            }
            else
            {
                logs.Add(new OperationLogEntry(
                    DateTimeOffset.Now,
                    OperationLogLevel.Error,
                    Operation,
                    path,
                    "文件缺失：起案服务器和生产服务器均不存在该文件。"));
            }
        }

        return new CheckRunResult(logs);
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
