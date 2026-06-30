using System.Xml.Linq;

namespace FtpBackupUploadTool.Tests;

internal static class MainWindowTests
{
    public static void MainWindowTitleIsFtpBuTool()
    {
        var xamlPath = FindRepositoryFile("src", "FtpBackupUploadTool.App", "MainWindow.xaml");
        var document = XDocument.Load(xamlPath);
        var title = document.Root?.Attribute("Title")?.Value;

        TestAssert.Equal("FTP BU Tool", title, "Main window title should match the product name");
    }

    public static void InitialRemoteRefreshTimeoutAllowsSlowCompanyFtpListing()
    {
        var codePath = FindRepositoryFile("src", "FtpBackupUploadTool.App", "MainWindow.xaml.cs");
        var code = File.ReadAllText(codePath);

        TestAssert.True(
            code.Contains("TimeSpan.FromSeconds(60)", StringComparison.Ordinal),
            "initial remote file pane refresh should allow slow company FTP directory listing");
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
