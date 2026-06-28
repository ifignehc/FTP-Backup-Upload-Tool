using FtpBackupUploadTool.Core.Paths;
using FtpBackupUploadTool.Core.Remote;

namespace FtpBackupUploadTool.Tests;

internal static class RemoteTests
{
    public static void LocalMirrorCanWriteAndRead()
    {
        var root = Path.Combine(Path.GetTempPath(), "ftp-tool-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var client = new LocalMirrorRemoteClient(root);
        var relative = RelativePath.Parse("css/site.css");

        using var source = new MemoryStream("body"u8.ToArray());
        client.UploadAsync(relative, source, CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.True(client.FileExistsAsync(relative, CancellationToken.None).GetAwaiter().GetResult(), "uploaded file should exist");
        using var downloaded = new MemoryStream();
        client.DownloadAsync(relative, downloaded, CancellationToken.None).GetAwaiter().GetResult();
        TestAssert.Equal("body", System.Text.Encoding.UTF8.GetString(downloaded.ToArray()), "downloaded content");
    }

    public static void CanceledUploadDoesNotCreateOrTruncateTargetFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "ftp-tool-tests", Guid.NewGuid().ToString("N"));
        var target = Path.Combine(root, "css", "site.css");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, "existing");
        var client = new LocalMirrorRemoteClient(root);
        var relative = RelativePath.Parse("css/site.css");
        using var source = new MemoryStream("new"u8.ToArray());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var failed = false;
        try
        {
            client.UploadAsync(relative, source, cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            failed = true;
        }

        TestAssert.True(failed, "already-canceled upload should throw");
        TestAssert.Equal("existing", File.ReadAllText(target), "canceled upload must not truncate existing file");
    }

    public static void CanceledDeleteDoesNotDeleteTargetFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "ftp-tool-tests", Guid.NewGuid().ToString("N"));
        var target = Path.Combine(root, "css", "site.css");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, "existing");
        var client = new LocalMirrorRemoteClient(root);
        var relative = RelativePath.Parse("css/site.css");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var failed = false;
        try
        {
            client.DeleteFileAsync(relative, cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            failed = true;
        }

        TestAssert.True(failed, "already-canceled delete should throw");
        TestAssert.True(File.Exists(target), "canceled delete must not delete target file");
    }

    public static void CanceledDownloadMissingFileThrowsCancellation()
    {
        var root = Path.Combine(Path.GetTempPath(), "ftp-tool-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var client = new LocalMirrorRemoteClient(root);
        var relative = RelativePath.Parse("css/missing.css");
        using var destination = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var canceled = false;
        try
        {
            client.DownloadAsync(relative, destination, cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        TestAssert.True(canceled, "already-canceled download should throw before missing file IO");
    }

    public static void LocalMirrorRejectsSiblingPrefixEscape()
    {
        var parent = Path.Combine(Path.GetTempPath(), "ftp-tool-tests", Guid.NewGuid().ToString("N"));
        var root = Path.Combine(parent, "root");
        var client = new LocalMirrorRemoteClient(root);
        var invalidPath = CreateRelativePath("../root2/site.css");
        var toFullPath = typeof(LocalMirrorRemoteClient).GetMethod(
            "ToFullPath",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        TestAssert.True(toFullPath is not null, "ToFullPath should exist");

        var failed = false;
        try
        {
            toFullPath!.Invoke(client, new object[] { invalidPath });
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is InvalidOperationException)
        {
            failed = true;
        }

        TestAssert.True(failed, "path resolving under sibling root2 must not be treated as inside root");
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
