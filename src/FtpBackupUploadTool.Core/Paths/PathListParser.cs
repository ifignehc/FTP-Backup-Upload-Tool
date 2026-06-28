namespace FtpBackupUploadTool.Core.Paths;

public static class PathListParser
{
    public static IReadOnlyList<RelativePath> Parse(string text)
    {
        return text
            .Split(new[] { "\r\n", "\n", "," }, StringSplitOptions.None)
            .Select(line => line.Trim().Trim(','))
            .Where(line => line.Length > 0)
            .Select(RelativePath.Parse)
            .DistinctBy(path => path.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
