using System.Text;
using FtpBackupUploadTool.Core.Logging;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;
using FtpBackupUploadTool.Core.Remote;
using FtpBackupUploadTool.Core.Services;

namespace FtpBackupUploadTool.Tests;

internal static class BackupServiceTests
{
    public static void BackupDownloadsExistingAndLogsNewFile()
    {
        var productionRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-prod", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(productionRoot, "css"));
        File.WriteAllText(Path.Combine(productionRoot, "css", "site.css"), "prod-body");

        var backupRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-backup", Guid.NewGuid().ToString("N"));
        var service = new BackupService(new LocalMirrorRemoteClient(productionRoot), new BackupLogWriter());
        var paths = new[] { RelativePath.Parse("css/site.css"), RelativePath.Parse("js/new.js") };

        var result = service.RunAsync(paths, backupRoot, "{yyyy}{MM}{dd}_{HH}{mm}{ss}_Backup", LogFieldOptions.All, CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.True(File.Exists(Path.Combine(result.BackupFolder, "css", "site.css")), "existing production file should be backed up");
        TestAssert.True(File.Exists(Path.Combine(result.BackupFolder, "backup-log.csv")), "backup log should exist");
        var logText = File.ReadAllText(Path.Combine(result.BackupFolder, "backup-log.csv"));
        TestAssert.True(logText.Contains("新文件，生产服务器不存在，跳过备份", StringComparison.Ordinal), "missing production file should be logged as new file");
        TestAssert.True(result.Logs.Any(log => log.Message == "新文件，跳过备份"), "missing production file should be logged with readable Chinese UI text");
    }

    public static void BackupLogWriterHonorsSelectedFields()
    {
        var logPath = Path.Combine(Path.GetTempPath(), "ftp-tool-backup-log", Guid.NewGuid().ToString("N"), "backup-log.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        var writer = new BackupLogWriter();
        var rows = new[]
        {
            new BackupLogRow(
                RelativePath.Parse("css/site.css"),
                "/prod/css/site.css",
                "/draft/css/site.css",
                @"D:\backup\css\site.css",
                12,
                new DateTimeOffset(2026, 6, 28, 12, 0, 0, TimeSpan.Zero),
                "BackedUp",
                "",
                "note")
        };

        writer.WriteAsync(logPath, rows, LogFieldOptions.RelativePath | LogFieldOptions.Result, CancellationToken.None).GetAwaiter().GetResult();

        var lines = File.ReadAllLines(logPath);
        TestAssert.Equal("RelativePath,Result", lines[0].TrimStart('\uFEFF'), "CSV header should contain only selected fields");
        TestAssert.True(!lines[0].Contains("ProductionFullPath", StringComparison.Ordinal), "CSV header should omit unselected production field");
        TestAssert.True(!lines[0].Contains("Note", StringComparison.Ordinal), "CSV header should omit unselected note field");
    }

    public static void BackupServiceWritesSelectedFullPathFields()
    {
        var productionRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-prod", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(productionRoot, "css"));
        File.WriteAllText(Path.Combine(productionRoot, "css", "site.css"), "prod-body");

        var backupRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-backup", Guid.NewGuid().ToString("N"));
        var service = new BackupService(new LocalMirrorRemoteClient(productionRoot), new BackupLogWriter());

        var result = service.RunAsync(
            new[] { RelativePath.Parse("css/site.css") },
            backupRoot,
            "Backup",
            LogFieldOptions.ProductionFullPath | LogFieldOptions.DraftFullPath | LogFieldOptions.LocalFullPath,
            CancellationToken.None,
            "/prod/root",
            "/draft/root").GetAwaiter().GetResult();

        var logText = File.ReadAllText(Path.Combine(result.BackupFolder, "backup-log.csv"));
        TestAssert.True(logText.Contains("/prod/root/css/site.css", StringComparison.Ordinal), "backup log should include production full path");
        TestAssert.True(logText.Contains("/draft/root/css/site.css", StringComparison.Ordinal), "backup log should include draft full path");
        TestAssert.True(logText.Contains(Path.Combine(result.BackupFolder, "css", "site.css"), StringComparison.Ordinal), "backup log should include local backup path");
    }

    public static void InvalidFolderTemplateThrowsBeforeCreatingOutsideBackupRoot()
    {
        var productionRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-prod", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(productionRoot);
        var backupRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-backup", Guid.NewGuid().ToString("N"));
        var siblingRoot = Path.Combine(Path.GetDirectoryName(backupRoot)!, "escape");
        if (Directory.Exists(siblingRoot))
        {
            Directory.Delete(siblingRoot, recursive: true);
        }

        var service = new BackupService(new LocalMirrorRemoteClient(productionRoot), new BackupLogWriter());

        var threw = false;
        try
        {
            service.RunAsync(
                Array.Empty<RelativePath>(),
                backupRoot,
                @"..\escape",
                LogFieldOptions.All,
                CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (ArgumentException)
        {
            threw = true;
        }

        TestAssert.True(threw, "invalid folder template should throw ArgumentException");
        TestAssert.True(!Directory.Exists(siblingRoot), "invalid folder template must not create outside backup root");
    }

    public static void RootedFolderTemplateThrowsBeforeCreatingOutsideBackupRoot()
    {
        var productionRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-prod", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(productionRoot);
        var backupRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-backup", Guid.NewGuid().ToString("N"));
        var outsideRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-rooted-escape", Guid.NewGuid().ToString("N"));
        var service = new BackupService(new LocalMirrorRemoteClient(productionRoot), new BackupLogWriter());

        var threw = false;
        try
        {
            service.RunAsync(
                Array.Empty<RelativePath>(),
                backupRoot,
                outsideRoot,
                LogFieldOptions.All,
                CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (ArgumentException)
        {
            threw = true;
        }

        TestAssert.True(threw, "rooted folder template should throw ArgumentException");
        TestAssert.True(!Directory.Exists(outsideRoot), "rooted folder template must not create outside backup root");
    }

    public static void AlreadyCanceledBackupDoesNotCreateBackupFolder()
    {
        var productionRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-prod", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(productionRoot);
        var backupRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-backup", Guid.NewGuid().ToString("N"));
        var service = new BackupService(new LocalMirrorRemoteClient(productionRoot), new BackupLogWriter());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var canceled = false;
        try
        {
            service.RunAsync(
                Array.Empty<RelativePath>(),
                backupRoot,
                "Backup",
                LogFieldOptions.All,
                cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        TestAssert.True(canceled, "already-canceled backup should throw");
        TestAssert.True(!Directory.Exists(backupRoot), "already-canceled backup must not create backup root or folder");
    }

    public static void BackupLogWriterCanceledBeforeReplacementPreservesExistingLog()
    {
        var logPath = Path.Combine(Path.GetTempPath(), "ftp-tool-backup-log", Guid.NewGuid().ToString("N"), "backup-log.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        File.WriteAllText(logPath, "existing-log", Encoding.UTF8);
        var writer = new BackupLogWriter();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var canceled = false;
        try
        {
            writer.WriteAsync(
                logPath,
                new[]
                {
                    new BackupLogRow(
                        RelativePath.Parse("css/site.css"),
                        "/prod/css/site.css",
                        string.Empty,
                        string.Empty,
                        null,
                        null,
                        "Skipped",
                        string.Empty,
                        "新文件，生产服务器不存在，跳过备份")
                },
                LogFieldOptions.All,
                cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        TestAssert.True(canceled, "canceled log write should throw");
        TestAssert.Equal("existing-log", File.ReadAllText(logPath, Encoding.UTF8), "canceled log write should preserve existing log");
    }

    public static void CanceledBackupDoesNotCreatePartialFile()
    {
        var productionRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-prod", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(productionRoot, "css"));
        File.WriteAllText(Path.Combine(productionRoot, "css", "site.css"), "prod-body");
        var backupRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-backup", Guid.NewGuid().ToString("N"));
        var service = new BackupService(new LocalMirrorRemoteClient(productionRoot), new BackupLogWriter());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var canceled = false;
        try
        {
            service.RunAsync(
                new[] { RelativePath.Parse("css/site.css") },
                backupRoot,
                "{yyyy}{MM}{dd}_{HH}{mm}{ss}_Backup",
                LogFieldOptions.All,
                cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        TestAssert.True(canceled, "already-canceled backup should throw");
        if (Directory.Exists(backupRoot))
        {
            var partialFiles = Directory.EnumerateFiles(backupRoot, "site.css", SearchOption.AllDirectories).ToArray();
            TestAssert.Equal(0, partialFiles.Length, "canceled backup must not create partial destination file");
        }
    }
}
