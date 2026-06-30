using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;
using FtpBackupUploadTool.Core.Remote;
using FtpBackupUploadTool.Core.Services;

namespace FtpBackupUploadTool.Tests;

internal static class CheckServiceTests
{
    public static void CheckReportsPathListStatusesAndLocalFilesMissingFromPathList()
    {
        var localRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-check-local", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(localRoot, "images"));
        File.WriteAllText(Path.Combine(localRoot, "images", "new.png"), "local-image");

        var production = new GuardedRemoteClient(
            existingFiles: new[] { "css/old.css" },
            entries: Array.Empty<FileEntry>());
        var draft = new GuardedRemoteClient(
            existingFiles: new[] { "css/site.css" },
            entries: new[]
            {
                new FileEntry(
                    RelativePath.Parse("css/site.css"),
                    false,
                    10,
                    new DateTimeOffset(2026, 6, 29, 15, 36, 29, TimeSpan.Zero))
            });
        var service = new CheckService(production, draft, localRoot);
        var paths = new[]
        {
            RelativePath.Parse("css/site.css"),
            RelativePath.Parse("css/old.css"),
            RelativePath.Parse("docs/missing.txt")
        };

        var result = service.RunAsync(paths, CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.Equal(4, result.Logs.Count, "check should report path list files plus local files missing from the path list");
        TestAssert.Equal(0, production.ListCalls, "check should not scan the production server");
        TestAssert.Equal(0, draft.ListCalls, "check should not scan the draft server");
        AssertLog(result.Logs, "css/site.css", OperationLogLevel.Normal, "文件更新");
        AssertLog(result.Logs, "css/site.css", OperationLogLevel.Normal, "2026-06-29 23:36:29");
        AssertLog(result.Logs, "css/old.css", OperationLogLevel.Warning, "新路径旧文件");
        AssertLog(result.Logs, "css/old.css", OperationLogLevel.Warning, "本次没有更新");
        AssertLog(result.Logs, "css/old.css", OperationLogLevel.Warning, "生产服务器有文件");
        AssertLog(result.Logs, "docs/missing.txt", OperationLogLevel.Error, "文件缺失");
        AssertLog(result.Logs, "images/new.png", OperationLogLevel.Error, "路径缺失");
    }

    public static void CheckDoesNotReportDraftFilesThatAreNotInPathList()
    {
        var production = new GuardedRemoteClient(
            existingFiles: new[] { "js/legacy.js" },
            entries: Array.Empty<FileEntry>());
        var draft = new GuardedRemoteClient(
            existingFiles: new[] { "css/site.css", "images/new.png" },
            entries: new[]
            {
                new FileEntry(RelativePath.Parse("css/site.css"), false, 10, DateTimeOffset.UtcNow),
                new FileEntry(RelativePath.Parse("images/new.png"), false, 20, DateTimeOffset.UtcNow)
            });
        var service = new CheckService(production, draft);
        var paths = new[]
        {
            RelativePath.Parse("css/site.css"),
            RelativePath.Parse("js/legacy.js"),
            RelativePath.Parse("docs/missing.txt")
        };

        var result = service.RunAsync(paths, CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.Equal(3, result.Logs.Count, "check should not scan or report draft files outside the path list");
        TestAssert.Equal(0, production.ListCalls, "check should not scan the production server");
        TestAssert.Equal(0, draft.ListCalls, "check should not scan the draft server");
        AssertLog(result.Logs, "css/site.css", OperationLogLevel.Normal, "文件更新");
        AssertNoLog(result.Logs, "images/new.png");
        AssertLog(result.Logs, "js/legacy.js", OperationLogLevel.Warning, "新路径旧文件");
        AssertLog(result.Logs, "docs/missing.txt", OperationLogLevel.Error, "文件缺失");
    }

    public static void CheckTreatsDuplicatePathsCaseInsensitively()
    {
        var draft = new GuardedRemoteClient(
            existingFiles: new[] { "css/site.css" },
            entries: new[]
            {
                new FileEntry(RelativePath.Parse("css/site.css"), false, 10, DateTimeOffset.UtcNow)
            });
        var production = new GuardedRemoteClient(existingFiles: Array.Empty<string>(), entries: Array.Empty<FileEntry>());
        var service = new CheckService(production, draft);
        var paths = new[]
        {
            RelativePath.Parse("CSS/SITE.CSS"),
            RelativePath.Parse("css/site.css")
        };

        var result = service.RunAsync(paths, CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.Equal(1, result.Logs.Count, "duplicate paths that differ only by case should be checked once");
        TestAssert.Equal(OperationLogLevel.Normal, result.Logs[0].Level, "duplicate path should match draft file case-insensitively");
        TestAssert.True(result.Logs[0].Message.Contains("文件更新", StringComparison.Ordinal), "duplicate path should be logged as an update");
    }

    private static void AssertLog(
        IReadOnlyList<OperationLogEntry> logs,
        string path,
        OperationLogLevel level,
        string messageFragment)
    {
        var log = logs.SingleOrDefault(entry => entry.Path?.Value.Equals(path, StringComparison.OrdinalIgnoreCase) == true);
        TestAssert.True(log is not null, $"expected log for {path}");
        TestAssert.Equal(level, log!.Level, $"{path} should have expected level");
        TestAssert.Equal("Check", log.Operation, $"{path} should have Check operation");
        TestAssert.True(log.Message.Contains(messageFragment, StringComparison.Ordinal), $"{path} should mention {messageFragment}");
    }

    private static void AssertNoLog(IReadOnlyList<OperationLogEntry> logs, string path)
    {
        var log = logs.SingleOrDefault(entry => entry.Path?.Value.Equals(path, StringComparison.OrdinalIgnoreCase) == true);
        TestAssert.True(log is null, $"expected no log for {path}");
    }

    private sealed class GuardedRemoteClient : IRemoteFileClient
    {
        private readonly Dictionary<string, FileEntry> entries;
        private readonly HashSet<string> existingFiles;

        public GuardedRemoteClient(IEnumerable<string> existingFiles, IEnumerable<FileEntry> entries)
        {
            this.entries = entries.ToDictionary(entry => entry.Path.Value, StringComparer.OrdinalIgnoreCase);
            this.existingFiles = new HashSet<string>(existingFiles, StringComparer.OrdinalIgnoreCase);
        }

        public int ListCalls { get; private set; }

        public Task<IReadOnlyList<FileEntry>> ListDirectoryAsync(RelativePath? directory, CancellationToken cancellationToken)
        {
            ListCalls++;
            throw new InvalidOperationException("Check should not list remote directories.");
        }

        public Task<IReadOnlyList<FileEntry>> ListRecursiveAsync(CancellationToken cancellationToken)
        {
            ListCalls++;
            throw new InvalidOperationException("Check should not recursively list remote files.");
        }

        public Task<bool> FileExistsAsync(RelativePath path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(existingFiles.Contains(path.Value));
        }

        public Task<bool> DirectoryExistsAsync(RelativePath path, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Check should not inspect remote directories.");
        }

        public Task<FileEntry?> GetFileEntryAsync(RelativePath path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.TryGetValue(path.Value, out var entry);
            return Task.FromResult(entry);
        }

        public Task DownloadAsync(RelativePath path, Stream destination, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Check should not download files.");
        }

        public Task UploadAsync(RelativePath path, Stream source, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Check should not upload files.");
        }

        public Task DeleteFileAsync(RelativePath path, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Check should not delete files.");
        }
    }
}
