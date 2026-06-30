using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;
using FtpBackupUploadTool.Core.Remote;
using FtpBackupUploadTool.Core.Services;
using FtpBackupUploadTool.Core.Logging;
using System.Text;

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

    public static void CheckLogWriterWritesGroupedMarkdownWithUpdatedFileDates()
    {
        var logDirectory = Path.Combine(Path.GetTempPath(), "ftp-tool-check-log", Guid.NewGuid().ToString("N"));
        var writer = new CheckLogWriter();
        var rows = new[]
        {
            new CheckLogRow(
                RelativePath.Parse("css/site.css"),
                OperationLogLevel.Normal,
                new DateTimeOffset(2026, 6, 29, 15, 36, 29, TimeSpan.Zero),
                "文件更新：起案服务器中已上传路径 css/site.css 对应的文件，修改日期：2026-06-29 23:36:29"),
            new CheckLogRow(
                RelativePath.Parse("css/old.css"),
                OperationLogLevel.Warning,
                null,
                "新路径旧文件：路径 css/old.css 本次没有更新，但是生产服务器有文件。"),
            new CheckLogRow(
                RelativePath.Parse("docs/missing.txt"),
                OperationLogLevel.Error,
                null,
                "文件缺失：路径 docs/missing.txt 对应文件缺失。")
        };

        var logPath = writer.WriteAsync(
            logDirectory,
            "20260629_183015_Check",
            rows,
            CancellationToken.None,
            new DateTimeOffset(2026, 6, 29, 10, 30, 15, TimeSpan.Zero)).GetAwaiter().GetResult();

        TestAssert.Equal(Path.Combine(logDirectory, "20260629_183015_Check.md"), logPath, "check log file name should use the configured template");
        var markdown = File.ReadAllText(logPath, Encoding.UTF8);
        TestAssert.True(markdown.Contains("# 20260629_183015_Check", StringComparison.Ordinal), "check log should use the rendered file name as title");
        TestAssert.True(markdown.Contains("- CheckTime: 2026-06-29 18:30:15", StringComparison.Ordinal), "check log should record the check time in the shared date format");
        TestAssert.True(markdown.Contains("## Updated Files", StringComparison.Ordinal), "check log should include updated files section");
        TestAssert.True(markdown.Contains("- FileDate: 2026-06-29 23:36:29", StringComparison.Ordinal), "updated files should include formatted file date");
        TestAssert.True(markdown.Contains("## Warnings", StringComparison.Ordinal), "check log should include warning section");
        TestAssert.True(markdown.Contains("css/old.css", StringComparison.Ordinal), "warning path should be listed");
        TestAssert.True(markdown.Contains("## Errors", StringComparison.Ordinal), "check log should include error section");
        TestAssert.True(markdown.Contains("docs/missing.txt", StringComparison.Ordinal), "error path should be listed");
    }

    public static void CheckLogWriterCreatesUniqueMarkdownForEachRun()
    {
        var logDirectory = Path.Combine(Path.GetTempPath(), "ftp-tool-check-log", Guid.NewGuid().ToString("N"));
        var writer = new CheckLogWriter();
        var rows = Array.Empty<CheckLogRow>();

        var first = writer.WriteAsync(logDirectory, "Check", rows, CancellationToken.None).GetAwaiter().GetResult();
        var second = writer.WriteAsync(logDirectory, "Check", rows, CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.True(File.Exists(first), "first check log should exist");
        TestAssert.True(File.Exists(second), "second check log should exist");
        TestAssert.True(!string.Equals(first, second, StringComparison.OrdinalIgnoreCase), "each check run should create a separate Markdown file");
    }

    public static void CheckLogWriterRendersFileNameTemplateWithBeijingTime()
    {
        var logDirectory = Path.Combine(Path.GetTempPath(), "ftp-tool-check-log", Guid.NewGuid().ToString("N"));
        var writer = new CheckLogWriter();

        var logPath = writer.WriteAsync(
            logDirectory,
            "{yyyy}{MM}{dd}_{HH}{mm}{ss}_Check",
            Array.Empty<CheckLogRow>(),
            CancellationToken.None,
            new DateTimeOffset(2026, 6, 29, 10, 30, 15, TimeSpan.Zero)).GetAwaiter().GetResult();

        TestAssert.Equal(
            Path.Combine(logDirectory, "20260629_183015_Check.md"),
            logPath,
            "check log file name tokens should use the same Beijing time display rule as log dates");
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
