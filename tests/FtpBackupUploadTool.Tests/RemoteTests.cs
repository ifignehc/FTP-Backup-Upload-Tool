using FtpBackupUploadTool.Core.Paths;
using FtpBackupUploadTool.Core.Remote;

namespace FtpBackupUploadTool.Tests;

internal static class RemoteTests
{
    public static void FtpPathBuildsRootUriFromTrimmedRoot()
    {
        var ftpPath = new FtpPath("ftp.example.com", 2121, "/draft/");

        var uri = ftpPath.For(null);

        TestAssert.Equal("ftp://ftp.example.com:2121/draft/", uri.AbsoluteUri, "root uri");
    }

    public static void FtpPathBuildsRootUriFromEmptyRoot()
    {
        var ftpPath = new FtpPath("ftp.example.com", 2121, "   ");

        var uri = ftpPath.For(null);

        TestAssert.Equal("ftp://ftp.example.com:2121/", uri.AbsoluteUri, "empty root uri");
    }

    public static void FtpPathBuildsRootUriFromSlashOnlyRoot()
    {
        var ftpPath = new FtpPath("ftp.example.com", 2121, "/");

        var uri = ftpPath.For(null);

        TestAssert.Equal("ftp://ftp.example.com:2121/", uri.AbsoluteUri, "slash-only root uri");
    }

    public static void FtpPathAppendsRelativePathSegments()
    {
        var ftpPath = new FtpPath("ftp.example.com", 2021, "production");

        var uri = ftpPath.For(RelativePath.Parse("css/site.css"));

        TestAssert.Equal("ftp://ftp.example.com:2021/production/css/site.css", uri.AbsoluteUri, "file uri");
    }

    public static void FtpPathEscapesEachSegmentWithoutEscapingSeparators()
    {
        var ftpPath = new FtpPath("ftp.example.com", 2021, "/草稿 root/");

        var uri = ftpPath.For(RelativePath.Parse("图片/hero image.png"));

        TestAssert.Equal("ftp://ftp.example.com:2021/%E8%8D%89%E7%A8%BF%20root/%E5%9B%BE%E7%89%87/hero%20image.png", uri.AbsoluteUri, "escaped file uri");
    }

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

    public static void MidStreamFailedUploadPreservesExistingTargetFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "ftp-tool-tests", Guid.NewGuid().ToString("N"));
        var target = Path.Combine(root, "css", "site.css");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, "existing");
        var client = new LocalMirrorRemoteClient(root);
        var relative = RelativePath.Parse("css/site.css");
        using var source = new FailingReadStream("new-body"u8.ToArray(), 3);

        var failed = false;
        try
        {
            client.UploadAsync(relative, source, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (IOException)
        {
            failed = true;
        }

        TestAssert.True(failed, "mid-stream failed upload should throw");
        TestAssert.Equal("existing", File.ReadAllText(target), "failed upload must preserve existing file");
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

    private sealed class FailingReadStream : Stream
    {
        private readonly byte[] _content;
        private readonly int _throwAfterBytes;
        private int _position;

        public FailingReadStream(byte[] content, int throwAfterBytes)
        {
            _content = content;
            _throwAfterBytes = throwAfterBytes;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _content.Length;
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= _throwAfterBytes)
            {
                throw new IOException("simulated read failure");
            }

            var remainingBeforeFailure = _throwAfterBytes - _position;
            var remainingContent = _content.Length - _position;
            var bytesRead = Math.Min(Math.Min(count, remainingBeforeFailure), remainingContent);
            Array.Copy(_content, _position, buffer, offset, bytesRead);
            _position += bytesRead;
            return bytesRead;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rented = new byte[buffer.Length];
            var bytesRead = Read(rented, 0, rented.Length);
            rented.AsMemory(0, bytesRead).CopyTo(buffer);
            return ValueTask.FromResult(bytesRead);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Read(buffer, offset, count));
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
