using FtpBackupUploadTool.Tests;

var tests = new Action[]
{
    PathTests.NormalizeRelativePaths,
    PathTests.RejectParentTraversal,
    PathTests.RejectRootedDrivePath,
    PathTests.RejectDriveQualifiedPath,
    PathTests.RejectRootedWindowsPath,
    PathTests.ParsePathListText,
    PathTests.ParseCommaSeparatedPathListText,
    RemoteTests.LocalMirrorCanWriteAndRead,
    RemoteTests.CanceledUploadDoesNotCreateOrTruncateTargetFile,
    RemoteTests.CanceledDeleteDoesNotDeleteTargetFile,
    RemoteTests.LocalMirrorRejectsSiblingPrefixEscape
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {test.Method.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"FAIL {test.Method.Name}: {ex.Message}");
        Console.WriteLine(failures[^1]);
    }
}

return failures.Count == 0 ? 0 : 1;
