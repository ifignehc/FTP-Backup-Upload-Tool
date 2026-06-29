namespace FtpBackupUploadTool.Tests;

internal static class PublishScriptTests
{
    public static void PublishScriptNamesPortableExeFtpBuTool()
    {
        var scriptPath = FindRepositoryFile("scripts", "publish-portable.ps1");
        var script = File.ReadAllText(scriptPath);

        TestAssert.True(
            script.Contains("'FTP BU Tool.exe'", StringComparison.Ordinal),
            "Publish script should name the generated portable exe FTP BU Tool.exe");
    }

    private static string FindRepositoryFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file.", Path.Combine(relativeParts));
    }
}
