using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;
using FtpBackupUploadTool.Core.Remote;
using FtpBackupUploadTool.Core.Services;

namespace FtpBackupUploadTool.Tests;

internal static class UploadServiceTests
{
    public static void UploadCopiesLocalFileToDraft()
    {
        var localRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-local", Guid.NewGuid().ToString("N"));
        var draftRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-draft", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(localRoot, "css"));
        Directory.CreateDirectory(Path.Combine(draftRoot, "css"));
        File.WriteAllText(Path.Combine(localRoot, "css", "site.css"), "local-body");

        var service = new UploadService(new LocalMirrorRemoteClient(draftRoot), localRoot);
        var result = service.RunAsync(new[] { RelativePath.Parse("css/site.css") }, CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.Equal("local-body", File.ReadAllText(Path.Combine(draftRoot, "css", "site.css")), "draft file should match local");
        TestAssert.Equal(1, result.Logs.Count, "one log entry");
        TestAssert.Equal(OperationLogLevel.Normal, result.Logs[0].Level, "successful upload should log normal");
        TestAssert.Equal("上传完成", result.Logs[0].Message, "successful upload message");
    }

    public static void UploadErrorsWhenDraftParentMissing()
    {
        var localRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-local", Guid.NewGuid().ToString("N"));
        var draftRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-draft", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(localRoot, "css"));
        File.WriteAllText(Path.Combine(localRoot, "css", "site.css"), "local-body");

        var service = new UploadService(new LocalMirrorRemoteClient(draftRoot), localRoot);
        var result = service.RunAsync(new[] { RelativePath.Parse("css/site.css") }, CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.Equal(OperationLogLevel.Error, result.Logs[0].Level, "missing parent should log error");
        TestAssert.Equal("起案服务器目标父文件夹不存在", result.Logs[0].Message, "missing parent message");
        TestAssert.True(!File.Exists(Path.Combine(draftRoot, "css", "site.css")), "missing draft parent must not create target file");
    }

    public static void CanceledUploadBeforeFileOpenDoesNotTruncateDraftFile()
    {
        var localRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-local", Guid.NewGuid().ToString("N"));
        var draftRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-draft", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(localRoot, "css"));
        Directory.CreateDirectory(Path.Combine(draftRoot, "css"));
        File.WriteAllText(Path.Combine(localRoot, "css", "site.css"), "local-body");
        var draftTarget = Path.Combine(draftRoot, "css", "site.css");
        File.WriteAllText(draftTarget, "existing");
        var service = new UploadService(new LocalMirrorRemoteClient(draftRoot), localRoot);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var canceled = false;
        try
        {
            service.RunAsync(new[] { RelativePath.Parse("css/site.css") }, cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        TestAssert.True(canceled, "already-canceled upload should throw");
        TestAssert.Equal("existing", File.ReadAllText(draftTarget), "canceled upload must not truncate existing draft file");
    }

    public static void ToLocalPathRejectsSiblingPrefixEscape()
    {
        var parent = Path.Combine(Path.GetTempPath(), "ftp-tool-local", Guid.NewGuid().ToString("N"));
        var localRoot = Path.Combine(parent, "root");
        var service = new UploadService(new LocalMirrorRemoteClient(Path.Combine(parent, "draft")), localRoot);
        var invalidPath = CreateRelativePath("../root2/site.css");
        var toLocalPath = typeof(UploadService).GetMethod(
            "ToLocalPath",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        TestAssert.True(toLocalPath is not null, "ToLocalPath should exist");

        var failed = false;
        try
        {
            toLocalPath!.Invoke(service, new object[] { invalidPath });
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is InvalidOperationException)
        {
            failed = true;
        }

        TestAssert.True(failed, "local path resolving under sibling root2 must not be treated as inside root");
    }

    private static RelativePath CreateRelativePath(string value)
    {
        var constructor = typeof(RelativePath).GetConstructor(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            new[] { typeof(string) },
            modifiers: null);
        TestAssert.True(constructor is not null, "RelativePath private constructor should exist");
        return (RelativePath)constructor!.Invoke(new object[] { value });
    }
}
