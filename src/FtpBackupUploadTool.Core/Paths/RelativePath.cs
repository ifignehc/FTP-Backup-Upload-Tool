namespace FtpBackupUploadTool.Core.Paths;

public sealed record RelativePath
{
    private RelativePath(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static RelativePath Parse(string input)
    {
        var trimmed = input.Trim();
        if (IsWindowsRootedOrDriveQualified(trimmed))
        {
            throw new ArgumentException("Path must be relative.", nameof(input));
        }

        var normalized = trimmed.Replace('\\', '/');
        while (normalized.StartsWith('/'))
        {
            normalized = normalized[1..];
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Path cannot be empty.", nameof(input));
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment == "." || segment == ".."))
        {
            throw new ArgumentException("Path cannot contain . or .. segments.", nameof(input));
        }

        return new RelativePath(string.Join('/', segments));
    }

    private static bool IsWindowsRootedOrDriveQualified(string path)
    {
        if (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':')
        {
            return true;
        }

        return path.StartsWith(@"\\") || path.StartsWith("//");
    }

    public override string ToString() => Value;
}
