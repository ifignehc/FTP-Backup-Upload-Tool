using FtpBackupUploadTool.Core.Paths;
using FtpBackupUploadTool.Core.Remote;
using System.Net;
using System.Net.Sockets;
using System.Text;

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

    public static void LocalMirrorListsImmediateDirectoryContents()
    {
        var root = Path.Combine(Path.GetTempPath(), "ftp-tool-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "images"));
        Directory.CreateDirectory(Path.Combine(root, "scripts"));
        File.WriteAllText(Path.Combine(root, "index.html"), "html");
        File.WriteAllText(Path.Combine(root, "images", "logo.png"), "png");
        var client = new LocalMirrorRemoteClient(root);

        var entries = client.ListDirectoryAsync(null, CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.Equal(3, entries.Count, "root should show immediate children only");
        TestAssert.True(entries.Any(entry => entry.IsDirectory && entry.Path.Value == "images"), "images directory should be visible");
        TestAssert.True(entries.Any(entry => entry.IsDirectory && entry.Path.Value == "scripts"), "scripts directory should be visible");
        TestAssert.True(entries.Any(entry => !entry.IsDirectory && entry.Path.Value == "index.html"), "root file should be visible");
        TestAssert.True(entries.All(entry => entry.Path.Value != "images/logo.png"), "nested file should not appear in root listing");
    }

    public static void LocalMirrorListsChildDirectoryWithFullRelativePaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "ftp-tool-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "images", "icons"));
        File.WriteAllText(Path.Combine(root, "images", "logo.png"), "png");
        var client = new LocalMirrorRemoteClient(root);

        var entries = client.ListDirectoryAsync(RelativePath.Parse("images"), CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.Equal(2, entries.Count, "child directory should show immediate children only");
        TestAssert.True(entries.Any(entry => entry.IsDirectory && entry.Path.Value == "images/icons"), "nested directory should keep full relative path");
        TestAssert.True(entries.Any(entry => !entry.IsDirectory && entry.Path.Value == "images/logo.png"), "nested file should keep full relative path");
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

    public static void FtpClientFallsBackToParentListingWhenSizeIsUnavailable()
    {
        using var server = new MetadataUnavailableFtpServer(
            "code/DDR5/SP16G/CLIENT/4SP66SR0_2V5.xml",
            "-rw-r--r-- 1 owner group 42 Jun 29 13:34 4SP66SR0_2V5.xml");
        var client = new FtpRemoteFileClient("127.0.0.1", server.Port, "/", "user", "pass");

        var entry = client.GetFileEntryAsync(
            RelativePath.Parse("code/DDR5/SP16G/CLIENT/4SP66SR0_2V5.xml"),
            CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.True(entry is not null, "FTP client should use parent LIST when direct metadata commands are unavailable: " + server.CommandLog);
        TestAssert.Equal(42L, entry!.Size, "listed file size should be used");
    }

    public static void FtpClientCanDisablePassiveModeForServersThatRequireActiveDataConnections()
    {
        var client = new FtpRemoteFileClient("172.27.3.41", 21, "/", "user", "pass", usePassive: false);
        var createRequest = typeof(FtpRemoteFileClient).GetMethod(
            "CreateRequest",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        TestAssert.True(createRequest is not null, "CreateRequest should exist");

        var request = (FtpWebRequest)createRequest!.Invoke(
            client,
            new object?[] { null, WebRequestMethods.Ftp.ListDirectory })!;

        TestAssert.True(!request.UsePassive, "client should be able to use active FTP mode for directory listings");
        TestAssert.Equal("172.27.3.41", request.RequestUri.Host, "request URI should still target the configured server");
        TestAssert.Equal("/", request.RequestUri.AbsolutePath, "request URI should still target the configured server root");
    }

    public static void FtpClientListsNamesWhenDetailedListFormatIsUnsupported()
    {
        using var server = new NamesOnlyFtpServer();
        var client = new FtpRemoteFileClient("127.0.0.1", server.Port, "/", "user", "pass");

        var entries = client.ListDirectoryAsync(null, CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.True(entries.Any(entry => entry.IsDirectory && entry.Path.Value == "folder"), "folder should be listed from NLST fallback");
        TestAssert.True(entries.Any(entry => !entry.IsDirectory && entry.Path.Value == "readme.txt"), "file should be listed from NLST fallback");
        TestAssert.True(server.RootListCount == 1, "root should not be recursively listed while classifying a directory: " + server.CommandLog);
    }

    public static void FtpClientUsesFileMetadataWhenNlstAcceptsFilePaths()
    {
        using var server = new NamesOnlyFtpServer(listFilePathAsName: true);
        var client = new FtpRemoteFileClient("127.0.0.1", server.Port, "/", "user", "pass");

        var entries = client.ListDirectoryAsync(null, CancellationToken.None).GetAwaiter().GetResult();
        var file = entries.First(entry => entry.Path.Value == "readme.txt");

        TestAssert.True(!file.IsDirectory, "NLST accepting a file path must not make the file look like a directory: " + server.CommandLog);
        TestAssert.Equal(5L, file.Size, "file size should come from FTP metadata");
        TestAssert.True(file.LastModified is not null, "file modified time should come from FTP metadata");
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

    private sealed class MetadataUnavailableFtpServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly string _filePath;
        private readonly string _parentPath;
        private readonly string _listLine;
        private readonly List<string> _commands = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _serverTask;

        public MetadataUnavailableFtpServer(string filePath, string listLine)
        {
            _filePath = "/" + filePath.Trim('/');
            var index = _filePath.LastIndexOf('/');
            _parentPath = index <= 0 ? "/" : _filePath[..index];
            _listLine = listLine;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _serverTask = Task.Run(AcceptLoop);
        }

        public int Port { get; }

        public string CommandLog
        {
            get
            {
                lock (_commands)
                {
                    return string.Join(" | ", _commands);
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            try
            {
                _serverTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
            }

            _cts.Dispose();
        }

        private async Task AcceptLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                _ = Task.Run(() => HandleClientAsync(client), _cts.Token);
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.ASCII))
            using (var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true })
            {
                await writer.WriteLineAsync("220 test ftp ready");
                TcpListener? dataListener = null;
                var currentDirectory = "/";

                while (!_cts.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync();
                    if (line is null)
                    {
                        return;
                    }

                    lock (_commands)
                    {
                        _commands.Add(line);
                    }

                    var command = line.Split(' ', 2)[0].ToUpperInvariant();
                    var argument = line.Contains(' ', StringComparison.Ordinal) ? line.Split(' ', 2)[1] : string.Empty;

                    switch (command)
                    {
                        case "USER":
                            await writer.WriteLineAsync("331 password required");
                            break;
                        case "PASS":
                            await writer.WriteLineAsync("230 logged in");
                            break;
                        case "SYST":
                            await writer.WriteLineAsync("215 UNIX Type: L8");
                            break;
                        case "FEAT":
                            await writer.WriteLineAsync("211 no features");
                            break;
                        case "PWD":
                            await writer.WriteLineAsync($"257 \"{currentDirectory}\" is current directory");
                            break;
                        case "CWD":
                            currentDirectory = ResolveFtpPath(currentDirectory, argument);
                            await writer.WriteLineAsync("250 directory changed");
                            break;
                        case "TYPE":
                            await writer.WriteLineAsync("200 type set");
                            break;
                        case "SIZE":
                        case "MDTM":
                            await writer.WriteLineAsync(ResolveFtpPath(currentDirectory, argument) == _filePath
                                ? "550 metadata unavailable"
                                : "550 file unavailable");
                            break;
                        case "PASV":
                            dataListener?.Stop();
                            dataListener = new TcpListener(IPAddress.Loopback, 0);
                            dataListener.Start();
                            var port = ((IPEndPoint)dataListener.LocalEndpoint).Port;
                            await writer.WriteLineAsync($"227 Entering Passive Mode (127,0,0,1,{port / 256},{port % 256})");
                            break;
                        case "LIST":
                            if (dataListener is null || ResolveFtpPath(currentDirectory, argument) != _parentPath)
                            {
                                await writer.WriteLineAsync("550 directory unavailable");
                                break;
                            }

                            await writer.WriteLineAsync("150 opening data connection");
                            using (var dataClient = await dataListener.AcceptTcpClientAsync(_cts.Token))
                            using (var dataStream = dataClient.GetStream())
                            using (var dataWriter = new StreamWriter(dataStream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true })
                            {
                                await dataWriter.WriteLineAsync(_listLine);
                            }

                            dataListener.Stop();
                            dataListener = null;
                            await writer.WriteLineAsync("226 transfer complete");
                            break;
                        case "QUIT":
                            await writer.WriteLineAsync("221 goodbye");
                            return;
                        default:
                            await writer.WriteLineAsync("200 ok");
                            break;
                    }
                }
            }
        }

        private static string ResolveFtpPath(string currentDirectory, string value)
        {
            var path = Uri.UnescapeDataString(value.Trim().Replace('\\', '/'));
            if (path.Length == 0)
            {
                return currentDirectory;
            }

            if (path.StartsWith("/", StringComparison.Ordinal))
            {
                return path;
            }

            return currentDirectory.TrimEnd('/') + "/" + path;
        }
    }

    private sealed class NamesOnlyFtpServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cts = new();
        private readonly List<string> _commands = new();
        private readonly bool _listFilePathAsName;
        private readonly Task _serverTask;

        public NamesOnlyFtpServer(bool listFilePathAsName = false)
        {
            _listFilePathAsName = listFilePathAsName;
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _serverTask = Task.Run(AcceptLoop);
        }

        public int Port { get; }

        public int RootListCount { get; private set; }

        public string CommandLog
        {
            get
            {
                lock (_commands)
                {
                    return string.Join(" | ", _commands);
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            try
            {
                _serverTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
            }

            _cts.Dispose();
        }

        private async Task AcceptLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                _ = Task.Run(() => HandleClientAsync(client), _cts.Token);
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.ASCII))
            using (var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true })
            {
                await writer.WriteLineAsync("220 test ftp ready");
                TcpListener? dataListener = null;

                while (!_cts.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync();
                    if (line is null)
                    {
                        return;
                    }

                    lock (_commands)
                    {
                        _commands.Add(line);
                    }

                    var command = line.Split(' ', 2)[0].ToUpperInvariant();
                    var argument = line.Contains(' ', StringComparison.Ordinal) ? line.Split(' ', 2)[1] : string.Empty;
                    var path = ResolveFtpPath("/", argument);

                    switch (command)
                    {
                        case "USER":
                            await writer.WriteLineAsync("331 password required");
                            break;
                        case "PASS":
                            await writer.WriteLineAsync("230 logged in");
                            break;
                        case "SYST":
                            await writer.WriteLineAsync("215 UNIX Type: L8");
                            break;
                        case "PWD":
                            await writer.WriteLineAsync("257 \"/\" is current directory");
                            break;
                        case "TYPE":
                            await writer.WriteLineAsync("200 type set");
                            break;
                        case "SIZE":
                            await writer.WriteLineAsync(path == "/readme.txt" ? "213 5" : "550 unavailable");
                            break;
                        case "MDTM":
                            await writer.WriteLineAsync(path == "/readme.txt" ? "213 20260630093000" : "550 unavailable");
                            break;
                        case "PASV":
                            dataListener?.Stop();
                            dataListener = new TcpListener(IPAddress.Loopback, 0);
                            dataListener.Start();
                            var port = ((IPEndPoint)dataListener.LocalEndpoint).Port;
                            await writer.WriteLineAsync($"227 Entering Passive Mode (127,0,0,1,{port / 256},{port % 256})");
                            break;
                        case "LIST":
                            if (path == "/")
                            {
                                RootListCount++;
                            }

                            await WriteDataAsync(writer, dataListener, new[] { "folder", "readme.txt" });
                            dataListener = null;
                            break;
                        case "NLST":
                            if (path == "/" || path.Length == 0)
                            {
                                await WriteDataAsync(writer, dataListener, new[] { "folder", "readme.txt" });
                            }
                            else if (path == "/folder")
                            {
                                await WriteDataAsync(writer, dataListener, Array.Empty<string>());
                            }
                            else if (_listFilePathAsName && path == "/readme.txt")
                            {
                                await WriteDataAsync(writer, dataListener, new[] { "readme.txt" });
                            }
                            else
                            {
                                await writer.WriteLineAsync("550 unavailable");
                            }

                            dataListener = null;
                            break;
                        case "QUIT":
                            await writer.WriteLineAsync("221 goodbye");
                            return;
                        default:
                            await writer.WriteLineAsync("200 ok");
                            break;
                    }
                }
            }
        }

        private static async Task WriteDataAsync(StreamWriter writer, TcpListener? dataListener, IReadOnlyList<string> lines)
        {
            if (dataListener is null)
            {
                await writer.WriteLineAsync("425 no data connection");
                return;
            }

            await writer.WriteLineAsync("150 opening data connection");
            using (var dataClient = await dataListener.AcceptTcpClientAsync())
            using (var dataStream = dataClient.GetStream())
            using (var dataWriter = new StreamWriter(dataStream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true })
            {
                foreach (var line in lines)
                {
                    await dataWriter.WriteLineAsync(line);
                }
            }

            dataListener.Stop();
            await writer.WriteLineAsync("226 transfer complete");
        }

        private static string ResolveFtpPath(string currentDirectory, string value)
        {
            var path = Uri.UnescapeDataString(value.Trim().Replace('\\', '/'));
            if (path.Length == 0)
            {
                return currentDirectory;
            }

            if (path.StartsWith("/", StringComparison.Ordinal))
            {
                return path;
            }

            return currentDirectory.TrimEnd('/') + "/" + path;
        }
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
