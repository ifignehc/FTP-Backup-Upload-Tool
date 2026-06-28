using FtpBackupUploadTool.Core.Logging;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;
using FtpBackupUploadTool.Core.Remote;

namespace FtpBackupUploadTool.Core.Services;

public sealed record BackupRunResult(string BackupFolder, IReadOnlyList<OperationLogEntry> Logs);

public sealed class BackupService
{
    private readonly IRemoteFileClient _production;
    private readonly BackupLogWriter _logWriter;

    public BackupService(IRemoteFileClient production, BackupLogWriter logWriter)
    {
        _production = production;
        _logWriter = logWriter;
    }

    public async Task<BackupRunResult> RunAsync(
        IReadOnlyList<RelativePath> paths,
        string backupRoot,
        string folderTemplate,
        LogFieldOptions logFields,
        CancellationToken cancellationToken)
    {
        var folder = Path.Combine(
            Environment.ExpandEnvironmentVariables(backupRoot),
            RenderFolderName(folderTemplate, DateTimeOffset.Now));
        Directory.CreateDirectory(folder);

        var logs = new List<OperationLogEntry>();
        var rows = new List<BackupLogRow>();

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = await _production.GetFileEntryAsync(path, cancellationToken);
            if (entry is null)
            {
                const string note = "新文件，生产服务器不存在，跳过备份";
                logs.Add(new OperationLogEntry(DateTimeOffset.Now, OperationLogLevel.Normal, "Backup", path, "新文件，跳过备份"));
                rows.Add(new BackupLogRow(path, path.Value, string.Empty, string.Empty, null, null, "Skipped", string.Empty, note));
                continue;
            }

            var destinationPath = GetDestinationPath(folder, path);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? folder);
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using var destination = File.Create(destinationPath);
                await _production.DownloadAsync(path, destination, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                TryDeletePartialFile(destinationPath);
                throw;
            }

            logs.Add(new OperationLogEntry(DateTimeOffset.Now, OperationLogLevel.Normal, "Backup", path, "备份完成"));
            rows.Add(new BackupLogRow(path, path.Value, string.Empty, destinationPath, entry.Size, entry.LastModified, "BackedUp", string.Empty, string.Empty));
        }

        await _logWriter.WriteAsync(Path.Combine(folder, "backup-log.csv"), rows, logFields, cancellationToken);
        return new BackupRunResult(folder, logs);
    }

    private static string RenderFolderName(string template, DateTimeOffset now)
    {
        return template
            .Replace("{yyyy}", now.ToString("yyyy"), StringComparison.Ordinal)
            .Replace("{MM}", now.ToString("MM"), StringComparison.Ordinal)
            .Replace("{dd}", now.ToString("dd"), StringComparison.Ordinal)
            .Replace("{HH}", now.ToString("HH"), StringComparison.Ordinal)
            .Replace("{mm}", now.ToString("mm"), StringComparison.Ordinal)
            .Replace("{ss}", now.ToString("ss"), StringComparison.Ordinal);
    }

    private static string GetDestinationPath(string backupFolder, RelativePath path)
    {
        var folder = Path.GetFullPath(backupFolder);
        var destination = Path.GetFullPath(Path.Combine(folder, path.Value.Replace('/', Path.DirectorySeparatorChar)));
        var folderWithSeparator = folder.EndsWith(Path.DirectorySeparatorChar)
            ? folder
            : folder + Path.DirectorySeparatorChar;

        if (!destination.StartsWith(folderWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Backup destination path escapes the backup folder.");
        }

        return destination;
    }

    private static void TryDeletePartialFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
