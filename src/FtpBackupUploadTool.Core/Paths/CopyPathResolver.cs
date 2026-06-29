namespace FtpBackupUploadTool.Core.Paths;

public static class CopyPathResolver
{
    public static RelativePath ResolveDestinationPath(string targetDirectory, RelativePath sourcePath)
    {
        var fileName = GetFileName(sourcePath);
        var normalizedTarget = NormalizeTargetDirectory(targetDirectory);

        return normalizedTarget.Length == 0
            ? RelativePath.Parse(fileName)
            : RelativePath.Parse($"{normalizedTarget}/{fileName}");
    }

    private static string NormalizeTargetDirectory(string? targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            return string.Empty;
        }

        return targetDirectory.Trim().Replace('\\', '/').Trim('/');
    }

    private static string GetFileName(RelativePath sourcePath)
    {
        var normalized = sourcePath.Value.Replace('\\', '/').Trim('/');
        var index = normalized.LastIndexOf('/');
        return index < 0 ? normalized : normalized[(index + 1)..];
    }
}
