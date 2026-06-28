using FtpBackupUploadTool.Core.Paths;

namespace FtpBackupUploadTool.Tests;

internal static class PathTests
{
    public static void NormalizeRelativePaths()
    {
        var path = RelativePath.Parse(@" /css\site.css ");
        TestAssert.Equal("css/site.css", path.Value, "relative path should be normalized");
    }

    public static void RejectParentTraversal()
    {
        var failed = false;
        try
        {
            RelativePath.Parse("../secret.txt");
        }
        catch (ArgumentException)
        {
            failed = true;
        }

        TestAssert.True(failed, "parent traversal must be rejected");
    }

    public static void ParsePathListText()
    {
        var input = "css/site.css\r\n\r\n images\\\\logo.png \r\n";
        var paths = PathListParser.Parse(input).Select(x => x.Value).ToArray();
        TestAssert.Equal(2, paths.Length, "blank lines should be ignored");
        TestAssert.Equal("css/site.css", paths[0], "first path");
        TestAssert.Equal("images/logo.png", paths[1], "second path");
    }
}
