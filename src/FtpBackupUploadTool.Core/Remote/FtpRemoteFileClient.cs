using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;

namespace FtpBackupUploadTool.Core.Remote;

public sealed class FtpRemoteFileClient : IRemoteFileClient
{
    private static readonly Regex UnixListRegex = new(
        @"^(?<type>[dl-])(?<permissions>[rwxstST-]{9})\s+\d+\s+\S+\s+\S+\s+(?<size>\d+)\s+(?<month>\w{3})\s+(?<day>\d{1,2})\s+(?<timeOrYear>\d{1,2}:\d{2}|\d{4})\s+(?<name>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WindowsListRegex = new(
        @"^(?<date>\d{2}-\d{2}-\d{2})\s+(?<time>\d{2}:\d{2}(?:AM|PM))\s+(?:(?<dir><DIR>)|(?<size>\d+))\s+(?<name>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly FtpPath _path;
    private readonly NetworkCredential _credentials;
    private readonly bool _usePassive;

    public FtpRemoteFileClient(string host, int port, string root, string userName, string password, bool usePassive = true)
        : this(new FtpPath(host, port, root), new NetworkCredential(userName, password), usePassive)
    {
    }

    public FtpRemoteFileClient(FtpPath path, NetworkCredential credentials, bool usePassive = true)
    {
        _path = path;
        _credentials = credentials;
        _usePassive = usePassive;
    }

    public async Task<IReadOnlyList<FileEntry>> ListDirectoryAsync(RelativePath? directory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var entries = await TryListDirectoryDetailsAsync(directory, cancellationToken);
            if (entries is not null)
            {
                return entries
                    .Select(entry => new FileEntry(Combine(directory, entry.Name), entry.IsDirectory, entry.Size, entry.LastModified))
                    .ToArray();
            }

            return await ListDirectoryByNamesAsync(directory, cancellationToken);
        }
        catch (WebException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw CreateDirectoryListException(directory, ex);
        }
    }

    public async Task<IReadOnlyList<FileEntry>> ListRecursiveAsync(CancellationToken cancellationToken)
    {
        var files = new List<FileEntry>();
        var visitedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await ListRecursiveAsync(directory: null, files, visitedDirectories, cancellationToken);
        return files;
    }

    public async Task<bool> FileExistsAsync(RelativePath path, CancellationToken cancellationToken)
    {
        return await GetFileEntryAsync(path, cancellationToken) is not null;
    }

    public async Task<bool> DirectoryExistsAsync(RelativePath path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await ListNamesAsync(path, cancellationToken);
            return true;
        }
        catch (WebException ex) when (IsNotFound(ex))
        {
            return false;
        }
    }

    public async Task<FileEntry?> GetFileEntryAsync(RelativePath path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entry = await TryGetFileEntryByMetadataAsync(path, cancellationToken);
        if (entry is not null)
        {
            return entry;
        }

        return await TryGetFileEntryFromParentListingAsync(path, cancellationToken);
    }

    public async Task DownloadAsync(RelativePath path, Stream destination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = CreateRequest(path, WebRequestMethods.Ftp.DownloadFile);
        using var response = await GetResponseAsync(request, cancellationToken);
        await using var source = response.GetResponseStream()
            ?? throw new InvalidOperationException("FTP 下载响应没有数据流。");
        await source.CopyToAsync(destination, cancellationToken);
    }

    public async Task UploadAsync(RelativePath path, Stream source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tempPath = GetTempUploadPath(path);
        try
        {
            var request = CreateRequest(tempPath, WebRequestMethods.Ftp.UploadFile);
            await using (var destination = await GetRequestStreamAsync(request, cancellationToken))
            {
                await source.CopyToAsync(destination, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            using (await GetResponseAsync(request, cancellationToken))
            {
            }

            cancellationToken.ThrowIfCancellationRequested();
            await ReplaceRemoteFileAsync(tempPath, path);
        }
        catch
        {
            await TryDeleteFileAsync(tempPath, CancellationToken.None);
            throw;
        }
    }

    public async Task DeleteFileAsync(RelativePath path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = CreateRequest(path, WebRequestMethods.Ftp.DeleteFile);
        using var response = await GetResponseAsync(request, cancellationToken);
    }

    private async Task ListRecursiveAsync(
        RelativePath? directory,
        List<FileEntry> files,
        HashSet<string> visitedDirectories,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var directoryKey = directory?.Value ?? string.Empty;
        if (!visitedDirectories.Add(directoryKey))
        {
            return;
        }

        var entries = await TryListDirectoryDetailsAsync(directory, cancellationToken);
        if (entries is null || entries.Count == 0)
        {
            await ListRecursiveByNamesAsync(directory, files, visitedDirectories, cancellationToken);
            return;
        }

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var childPath = Combine(directory, entry.Name);
            if (entry.IsDirectory)
            {
                await ListRecursiveAsync(childPath, files, visitedDirectories, cancellationToken);
            }
            else
            {
                files.Add(new FileEntry(childPath, false, entry.Size, entry.LastModified));
            }
        }
    }

    private async Task ListRecursiveByNamesAsync(
        RelativePath? directory,
        List<FileEntry> files,
        HashSet<string> visitedDirectories,
        CancellationToken cancellationToken)
    {
        var names = await ListNamesAsync(directory, cancellationToken);
        foreach (var name in names)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var childPath = Combine(directory, name);
            if (await DirectoryExistsAsync(childPath, cancellationToken))
            {
                await ListRecursiveAsync(childPath, files, visitedDirectories, cancellationToken);
                continue;
            }

            files.Add(await TryGetFileEntryByMetadataAsync(childPath, cancellationToken)
                ?? new FileEntry(childPath, false, 0, null));
        }
    }

    private async Task<IReadOnlyList<FileEntry>> ListDirectoryByNamesAsync(
        RelativePath? directory,
        CancellationToken cancellationToken)
    {
        var result = new List<FileEntry>();
        var names = await ListNamesAsync(directory, cancellationToken);
        foreach (var name in names)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var childPath = Combine(directory, name);
            if (await DirectoryExistsAsync(childPath, cancellationToken))
            {
                result.Add(new FileEntry(childPath, true, 0, null));
                continue;
            }

            result.Add(await TryGetFileEntryByMetadataAsync(childPath, cancellationToken)
                ?? new FileEntry(childPath, false, 0, null));
        }

        return result;
    }

    private async Task<FileEntry?> TryGetFileEntryByMetadataAsync(RelativePath path, CancellationToken cancellationToken)
    {
        long size;
        try
        {
            size = await GetFileSizeAsync(path, cancellationToken);
        }
        catch (WebException ex) when (IsNotFound(ex))
        {
            return null;
        }

        DateTimeOffset? lastModified;
        try
        {
            lastModified = await GetLastModifiedAsync(path, cancellationToken);
        }
        catch (WebException ex) when (IsNotFound(ex))
        {
            lastModified = null;
        }

        return new FileEntry(path, false, size, lastModified);
    }

    private async Task<IReadOnlyList<DirectoryListEntry>?> TryListDirectoryDetailsAsync(
        RelativePath? directory,
        CancellationToken cancellationToken)
    {
        var lines = await ReadLinesAsync(directory, WebRequestMethods.Ftp.ListDirectoryDetails, cancellationToken);
        var entries = new List<DirectoryListEntry>();

        foreach (var line in lines)
        {
            var entry = TryParseDirectoryListEntry(line);
            if (entry is null)
            {
                if (!IsSelfOrParent(GetLastPathSegment(line)))
                {
                    return null;
                }

                continue;
            }

            if (!IsSelfOrParent(entry.Name))
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    private async Task<IReadOnlyList<string>> ListNamesAsync(RelativePath? directory, CancellationToken cancellationToken)
    {
        var lines = await ReadLinesAsync(directory, WebRequestMethods.Ftp.ListDirectory, cancellationToken);
        return lines
            .Select(GetLastPathSegment)
            .Where(name => !string.IsNullOrWhiteSpace(name) && !IsSelfOrParent(name))
            .ToArray();
    }

    private async Task<FileEntry?> TryGetFileEntryFromParentListingAsync(RelativePath path, CancellationToken cancellationToken)
    {
        var parent = GetParentPath(path);
        var fileName = GetLastPathSegment(path.Value);

        IReadOnlyList<FileEntry> entries;
        try
        {
            entries = await ListDirectoryAsync(parent, cancellationToken);
        }
        catch (WebException ex) when (IsNotFound(ex))
        {
            return null;
        }

        return entries.FirstOrDefault(entry =>
            !entry.IsDirectory
            && entry.Path.Value.Equals(path.Value, StringComparison.Ordinal)
            && GetLastPathSegment(entry.Path.Value).Equals(fileName, StringComparison.Ordinal));
    }

    private async Task<IReadOnlyList<string>> ReadLinesAsync(
        RelativePath? path,
        string method,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = CreateRequest(path, method);
        using var response = await GetResponseAsync(request, cancellationToken);
        using var stream = response.GetResponseStream()
            ?? throw new InvalidOperationException("FTP 列表响应没有数据流。");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(cancellationToken);

        return text
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();
    }

    private async Task<long> GetFileSizeAsync(RelativePath path, CancellationToken cancellationToken)
    {
        var request = CreateRequest(path, WebRequestMethods.Ftp.GetFileSize);
        using var response = await GetResponseAsync(request, cancellationToken);
        return response.ContentLength;
    }

    private async Task<DateTimeOffset?> GetLastModifiedAsync(RelativePath path, CancellationToken cancellationToken)
    {
        var request = CreateRequest(path, WebRequestMethods.Ftp.GetDateTimestamp);
        using var response = await GetResponseAsync(request, cancellationToken);
        return response.LastModified == DateTime.MinValue ? null : new DateTimeOffset(response.LastModified);
    }

#pragma warning disable SYSLIB0014

    private FtpWebRequest CreateRequest(RelativePath? path, string method)
    {
        var request = (FtpWebRequest)WebRequest.Create(_path.For(path));
        request.Method = method;
        request.Credentials = _credentials;
        request.UseBinary = true;
        request.UsePassive = _usePassive;
        request.KeepAlive = false;
        return request;
    }

    private InvalidOperationException CreateDirectoryListException(RelativePath? directory, WebException exception)
    {
        var uri = _path.For(directory);
        var mode = _usePassive ? "Passive" : "Active";
        var details = exception.Response is FtpWebResponse response
            ? $"{response.StatusCode} {response.StatusDescription}".Trim()
            : exception.Status.ToString();
        return new InvalidOperationException($"FTP list failed for {uri} ({mode} mode): {details}", exception);
    }

    private async Task ReplaceRemoteFileAsync(RelativePath tempPath, RelativePath finalPath)
    {
        try
        {
            await RenameAsync(tempPath, finalPath, CancellationToken.None);
            return;
        }
        catch (WebException ex) when (IsUnavailableForRename(ex))
        {
        }

        var backupPath = GetTempUploadPath(finalPath);
        var movedExistingFile = false;

        try
        {
            await RenameAsync(finalPath, backupPath, CancellationToken.None);
            movedExistingFile = true;
            await RenameAsync(tempPath, finalPath, CancellationToken.None);
            await TryDeleteFileAsync(backupPath, CancellationToken.None);
        }
        catch
        {
            if (movedExistingFile)
            {
                await TryRestoreBackupAsync(backupPath, finalPath);
            }

            throw;
        }
    }

    private async Task RenameAsync(RelativePath sourcePath, RelativePath targetPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = CreateRequest(sourcePath, WebRequestMethods.Ftp.Rename);
        request.RenameTo = GetLastPathSegment(targetPath.Value);
        using var response = await GetResponseAsync(request, cancellationToken);
    }

    private async Task TryDeleteFileAsync(RelativePath path, CancellationToken cancellationToken)
    {
        try
        {
            await DeleteFileAsync(path, cancellationToken);
        }
        catch
        {
        }
    }

    private async Task TryRestoreBackupAsync(RelativePath backupPath, RelativePath finalPath)
    {
        try
        {
            if (!await FileExistsAsync(finalPath, CancellationToken.None)
                && await FileExistsAsync(backupPath, CancellationToken.None))
            {
                await RenameAsync(backupPath, finalPath, CancellationToken.None);
            }
        }
        catch
        {
        }
    }

    private static async Task<FtpWebResponse> GetResponseAsync(FtpWebRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var registration = cancellationToken.Register(static state => ((FtpWebRequest)state!).Abort(), request);

        try
        {
            return (FtpWebResponse)await request.GetResponseAsync();
        }
        catch (WebException ex) when (cancellationToken.IsCancellationRequested && ex.Status == WebExceptionStatus.RequestCanceled)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private static async Task<Stream> GetRequestStreamAsync(FtpWebRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var registration = cancellationToken.Register(static state => ((FtpWebRequest)state!).Abort(), request);

        try
        {
            return await request.GetRequestStreamAsync();
        }
        catch (WebException ex) when (cancellationToken.IsCancellationRequested && ex.Status == WebExceptionStatus.RequestCanceled)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private static bool IsNotFound(WebException exception)
    {
        if (exception.Status != WebExceptionStatus.ProtocolError)
        {
            return false;
        }

        return exception.Response is FtpWebResponse response
            && response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable;
    }

    private static bool IsUnavailableForRename(WebException exception)
    {
        return exception.Status == WebExceptionStatus.ProtocolError
            && exception.Response is FtpWebResponse response
            && response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable;
    }

#pragma warning restore SYSLIB0014

    private static DirectoryListEntry? TryParseDirectoryListEntry(string line)
    {
        var windowsMatch = WindowsListRegex.Match(line);
        if (windowsMatch.Success)
        {
            var name = GetLastPathSegment(windowsMatch.Groups["name"].Value);
            var isDirectory = windowsMatch.Groups["dir"].Success;
            var size = isDirectory ? 0 : long.Parse(windowsMatch.Groups["size"].Value, CultureInfo.InvariantCulture);
            var lastModified = TryParseWindowsTimestamp(windowsMatch);
            return new DirectoryListEntry(name, isDirectory, size, lastModified);
        }

        var unixMatch = UnixListRegex.Match(line);
        if (unixMatch.Success)
        {
            var name = GetLastPathSegment(unixMatch.Groups["name"].Value);
            var isDirectory = unixMatch.Groups["type"].Value == "d";
            var size = long.Parse(unixMatch.Groups["size"].Value, CultureInfo.InvariantCulture);
            var lastModified = TryParseUnixTimestamp(unixMatch);
            return new DirectoryListEntry(name, isDirectory, size, lastModified);
        }

        return null;
    }

    private static DateTimeOffset? TryParseWindowsTimestamp(Match match)
    {
        var value = $"{match.Groups["date"].Value} {match.Groups["time"].Value}";
        if (DateTime.TryParseExact(
                value,
                "MM-dd-yy hh:mmtt",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var dateTime))
        {
            return new DateTimeOffset(dateTime);
        }

        return null;
    }

    private static DateTimeOffset? TryParseUnixTimestamp(Match match)
    {
        var value = $"{match.Groups["month"].Value} {match.Groups["day"].Value} {match.Groups["timeOrYear"].Value}";
        var format = match.Groups["timeOrYear"].Value.Contains(':') ? "MMM d HH:mm" : "MMM d yyyy";
        if (!DateTime.TryParseExact(
                value,
                format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var dateTime))
        {
            return null;
        }

        if (format == "MMM d HH:mm")
        {
            dateTime = new DateTime(
                DateTimeOffset.Now.Year,
                dateTime.Month,
                dateTime.Day,
                dateTime.Hour,
                dateTime.Minute,
                0,
                DateTimeKind.Local);
        }

        return new DateTimeOffset(dateTime);
    }

    private static RelativePath Combine(RelativePath? directory, string name)
    {
        return directory is null
            ? RelativePath.Parse(name)
            : RelativePath.Parse($"{directory.Value}/{name}");
    }

    private static RelativePath GetTempUploadPath(RelativePath path)
    {
        var index = path.Value.LastIndexOf('/');
        var directory = index < 0 ? string.Empty : path.Value[..(index + 1)];
        var fileName = index < 0 ? path.Value : path.Value[(index + 1)..];
        return RelativePath.Parse($"{directory}.{fileName}.{Guid.NewGuid():N}.tmp");
    }

    private static RelativePath? GetParentPath(RelativePath path)
    {
        var index = path.Value.LastIndexOf('/');
        return index < 0 ? null : RelativePath.Parse(path.Value[..index]);
    }

    private static string GetLastPathSegment(string value)
    {
        var normalized = value.Trim().TrimEnd('/').Replace('\\', '/');
        var index = normalized.LastIndexOf('/');
        return index < 0 ? normalized : normalized[(index + 1)..];
    }

    private static bool IsSelfOrParent(string value)
    {
        return value is "." or "..";
    }

    private sealed record DirectoryListEntry(string Name, bool IsDirectory, long Size, DateTimeOffset? LastModified);
}
