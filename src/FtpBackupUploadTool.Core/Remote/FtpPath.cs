using FtpBackupUploadTool.Core.Paths;

namespace FtpBackupUploadTool.Core.Remote;

public sealed class FtpPath
{
    private readonly string _authority;
    private readonly string[] _rootSegments;

    public FtpPath(string host, int port, string root)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("FTP host cannot be empty.", nameof(host));
        }

        if (port <= 0 || port > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "FTP port must be between 1 and 65535.");
        }

        var builder = new UriBuilder(Uri.UriSchemeFtp, host, port);
        _authority = $"{Uri.UriSchemeFtp}://{FormatHost(builder.Host)}:{port}";
        _rootSegments = SplitSegments(root);
    }

    public Uri For(RelativePath? path)
    {
        var segments = new List<string>(_rootSegments.Length + 8);
        segments.AddRange(_rootSegments);

        if (path is not null)
        {
            segments.AddRange(SplitSegments(path.Value));
        }

        var escapedPath = "/" + string.Join("/", segments.Select(Uri.EscapeDataString));
        if (path is null)
        {
            escapedPath += "/";
        }

        return new Uri(_authority + escapedPath);
    }

    private static string[] SplitSegments(string value)
    {
        return value
            .Trim()
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string FormatHost(string host)
    {
        var unwrapped = host.Trim('[', ']');
        return unwrapped.Contains(':', StringComparison.Ordinal) ? $"[{unwrapped}]" : unwrapped;
    }
}
