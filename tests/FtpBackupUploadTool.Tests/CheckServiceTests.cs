using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;
using FtpBackupUploadTool.Core.Remote;
using FtpBackupUploadTool.Core.Services;

namespace FtpBackupUploadTool.Tests;

internal static class CheckServiceTests
{
    public static void CheckDoesNotRecursivelyListDraftServer()
    {
        var production = new GuardedRemoteClient(existingFiles: new[] { "css/old.css" });
        var draft = new GuardedRemoteClient(existingFiles: new[] { "css/site.css" });
        var service = new CheckService(production, draft);
        var paths = new[]
        {
            RelativePath.Parse("css/site.css"),
            RelativePath.Parse("css/old.css"),
            RelativePath.Parse("docs/missing.txt")
        };

        var result = service.RunAsync(paths, CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.Equal(3, result.Logs.Count, "check should only emit logs for paths in the list");
        TestAssert.Equal(3, draft.FileExistsCalls, "draft server should be probed once per unique path");
        TestAssert.Equal(2, production.FileExistsCalls, "production server should only be probed when draft is missing");
        AssertLog(result.Logs, "css/site.css", OperationLogLevel.Normal, string.Empty);
        AssertLog(result.Logs, "css/old.css", OperationLogLevel.Warning, string.Empty);
        AssertLog(result.Logs, "docs/missing.txt", OperationLogLevel.Error, string.Empty);
    }

    public static void CheckIgnoresDraftFilesThatAreNotInPathList()
    {
        var draftRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-check-draft", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(draftRoot, "css"));
        Directory.CreateDirectory(Path.Combine(draftRoot, "images"));
        File.WriteAllText(Path.Combine(draftRoot, "css", "site.css"), "draft-body");
        File.WriteAllText(Path.Combine(draftRoot, "images", "new.png"), "draft-image");

        var productionRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-check-prod", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(productionRoot, "js"));
        File.WriteAllText(Path.Combine(productionRoot, "js", "legacy.js"), "prod-script");

        var service = new CheckService(
            new LocalMirrorRemoteClient(productionRoot),
            new LocalMirrorRemoteClient(draftRoot));
        var paths = new[]
        {
            RelativePath.Parse("css/site.css"),
            RelativePath.Parse("js/legacy.js"),
            RelativePath.Parse("docs/missing.txt")
        };

        var result = service.RunAsync(paths, CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.Equal(3, result.Logs.Count, "check should only emit logs for paths in the list");
        AssertLog(result.Logs, "css/site.css", OperationLogLevel.Normal, "文件更新");
        AssertNoLog(result.Logs, "images/new.png");
        AssertLog(result.Logs, "js/legacy.js", OperationLogLevel.Warning, "新路径");
        AssertLog(result.Logs, "docs/missing.txt", OperationLogLevel.Error, "文件缺失");
    }

    public static void CheckTreatsDuplicatePathsCaseInsensitively()
    {
        var draftRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-check-draft", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(draftRoot, "css"));
        File.WriteAllText(Path.Combine(draftRoot, "css", "site.css"), "draft-body");

        var productionRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-check-prod", Guid.NewGuid().ToString("N"));
        var service = new CheckService(
            new LocalMirrorRemoteClient(productionRoot),
            new LocalMirrorRemoteClient(draftRoot));
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
        private readonly HashSet<string> existingFiles;

        public GuardedRemoteClient(IEnumerable<string> existingFiles)
        {
            this.existingFiles = new HashSet<string>(existingFiles, StringComparer.OrdinalIgnoreCase);
        }

        public int FileExistsCalls { get; private set; }

        public Task<IReadOnlyList<FileEntry>> ListDirectoryAsync(RelativePath? directory, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Check should not list directories.");
        }

        public Task<IReadOnlyList<FileEntry>> ListRecursiveAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Check should not recursively list the server.");
        }

        public Task<bool> FileExistsAsync(RelativePath path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileExistsCalls++;
            return Task.FromResult(existingFiles.Contains(path.Value));
        }

        public Task<bool> DirectoryExistsAsync(RelativePath path, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Check should not inspect directories.");
        }

        public Task<FileEntry?> GetFileEntryAsync(RelativePath path, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Check should not read file entries.");
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
