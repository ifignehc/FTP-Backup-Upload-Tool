using System.Xml.Linq;

namespace FtpBackupUploadTool.Tests;

internal static class AppProjectTests
{
    public static void AppProjectEmbedsWindowIconResource()
    {
        var projectPath = FindRepositoryFile("src", "FtpBackupUploadTool.App", "FtpBackupUploadTool.App.csproj");
        var project = XDocument.Load(projectPath);
        var hasIconResource = project
            .Descendants("Resource")
            .Any(element => string.Equals(
                (string?)element.Attribute("Include"),
                @"Assets\AppIcon.ico",
                StringComparison.OrdinalIgnoreCase));

        TestAssert.True(hasIconResource, "App project should embed the window icon as a WPF resource");
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
