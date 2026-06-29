using FtpBackupUploadTool.Core.Paths;

namespace FtpBackupUploadTool.Tests;

internal static class CopyPathTests
{
    public static void CopyIntoDraftCurrentDirectoryUsesSourceFileName()
    {
        var result = CopyPathResolver.ResolveDestinationPath("/release/assets", RelativePath.Parse("css/site.css"));

        TestAssert.Equal("release/assets/site.css", result.Value, "copy should use target pane directory with source file name");
    }

    public static void CopyIntoRootUsesSourceFileNameOnly()
    {
        var result = CopyPathResolver.ResolveDestinationPath("/", RelativePath.Parse("css/site.css"));

        TestAssert.Equal("site.css", result.Value, "copy into root should not preserve source directory");
    }
}
