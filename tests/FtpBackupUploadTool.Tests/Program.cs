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
    ConfigTests.PasswordRoundTripUsesProtector,
    ConfigTests.DpapiPasswordRoundTripForCurrentUser,
    ConfigTests.ConfigRoundTripPreservesProcess,
    ConfigTests.CanceledSavePreservesExistingConfig,
    ConfigTests.FailedReplacePreservesExistingConfig,
    RemoteTests.LocalMirrorCanWriteAndRead,
    RemoteTests.CanceledUploadDoesNotCreateOrTruncateTargetFile,
    RemoteTests.MidStreamFailedUploadPreservesExistingTargetFile,
    RemoteTests.CanceledDeleteDoesNotDeleteTargetFile,
    RemoteTests.CanceledDownloadMissingFileThrowsCancellation,
    RemoteTests.LocalMirrorRejectsSiblingPrefixEscape,
    BackupServiceTests.BackupDownloadsExistingAndLogsNewFile,
    BackupServiceTests.BackupLogWriterHonorsSelectedFields,
    BackupServiceTests.InvalidFolderTemplateThrowsBeforeCreatingOutsideBackupRoot,
    BackupServiceTests.RootedFolderTemplateThrowsBeforeCreatingOutsideBackupRoot,
    BackupServiceTests.AlreadyCanceledBackupDoesNotCreateBackupFolder,
    BackupServiceTests.BackupLogWriterCanceledBeforeReplacementPreservesExistingLog,
    BackupServiceTests.CanceledBackupDoesNotCreatePartialFile,
    UploadServiceTests.UploadCopiesLocalFileToDraft,
    UploadServiceTests.UploadCopiesRootLevelLocalFileToDraftRoot,
    UploadServiceTests.UploadErrorsWhenDraftParentMissing,
    UploadServiceTests.CanceledUploadBeforeFileOpenDoesNotTruncateDraftFile,
    UploadServiceTests.ToLocalPathRejectsSiblingPrefixEscape
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
