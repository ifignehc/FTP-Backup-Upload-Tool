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
}
