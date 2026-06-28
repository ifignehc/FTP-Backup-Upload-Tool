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
        var normalized = input.Trim().Replace('\\', '/');
        while (normalized.StartsWith('/'))
        {
            normalized = normalized[1..];
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("路径不能为空。", nameof(input));
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment == "." || segment == ".."))
        {
            throw new ArgumentException("路径不能包含 . 或 ..。", nameof(input));
        }

        return new RelativePath(string.Join('/', segments));
    }

    public override string ToString() => Value;
}
