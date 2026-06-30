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
    CopyPathTests.CopyIntoDraftCurrentDirectoryUsesSourceFileName,
    CopyPathTests.CopyIntoRootUsesSourceFileNameOnly,
    ConfigTests.PasswordRoundTripUsesProtector,
    ConfigTests.DpapiPasswordRoundTripForCurrentUser,
    ConfigTests.ConfigRoundTripPreservesProcess,
    ConfigTests.LoadingLegacyConfigDefaultsFtpServersToPassiveMode,
    ConfigTests.CanceledSavePreservesExistingConfig,
    ConfigTests.FailedReplacePreservesExistingConfig,
    SettingsViewModelTests.LoadsAllSavedProcessesAndSelectedProcessFields,
    SettingsViewModelTests.SelectingSavedProcessLoadsItsServerFields,
    SettingsViewModelTests.EmptyPasswordKeepsExistingSavedPassword,
    SettingsViewModelTests.SavedPasswordsEnableRememberPasswordAndCanBeDisplayed,
    SettingsViewModelTests.TurningOffRememberPasswordClearsSavedPasswords,
    SettingsViewModelTests.TypedPasswordsAreSavedEvenWhenRememberWasNotPrechecked,
    SettingsViewModelTests.AddProcessCreatesBlankDraftWithUniqueName,
    SettingsViewModelTests.CopySelectedProcessCreatesDraftWithCurrentFields,
    SettingsViewModelTests.DeleteSelectedProcessRemovesItAndSelectsRemainingProcess,
    SettingsViewModelTests.BuildsAndLoadsActiveFtpModeSettings,
    MainWindowTests.MainWindowTitleIsFtpBuTool,
    MainWindowTests.InitialRemoteRefreshTimeoutAllowsSlowCompanyFtpListing,
    PublishScriptTests.PublishScriptNamesPortableExeFtpBuTool,
    AppProjectTests.AppProjectEmbedsWindowIconResource,
    LogForegroundConverterTests.WarningAndErrorLogsUseDistinctForegroundColors,
    FileEntryTests.FileEntryFormatsLastModifiedLikeBackupLog,
    FileEntryTests.FileEntryFormatsMissingLastModifiedAsBlank,
    MainViewModelTests.StartsWithEmptyPathList,
    ProcessRuntimeFactoryTests.EmptySavedPasswordsCreateRuntimeWithBlankPasswords,
    RemoteTests.FtpPathBuildsRootUriFromTrimmedRoot,
    RemoteTests.FtpPathBuildsRootUriFromEmptyRoot,
    RemoteTests.FtpPathBuildsRootUriFromSlashOnlyRoot,
    RemoteTests.FtpPathAppendsRelativePathSegments,
    RemoteTests.FtpPathEscapesEachSegmentWithoutEscapingSeparators,
    RemoteTests.LocalMirrorCanWriteAndRead,
    RemoteTests.LocalMirrorListsImmediateDirectoryContents,
    RemoteTests.LocalMirrorListsChildDirectoryWithFullRelativePaths,
    RemoteTests.CanceledUploadDoesNotCreateOrTruncateTargetFile,
    RemoteTests.MidStreamFailedUploadPreservesExistingTargetFile,
    RemoteTests.CanceledDeleteDoesNotDeleteTargetFile,
    RemoteTests.CanceledDownloadMissingFileThrowsCancellation,
    RemoteTests.LocalMirrorRejectsSiblingPrefixEscape,
    RemoteTests.FtpClientFallsBackToParentListingWhenSizeIsUnavailable,
    RemoteTests.FtpClientCanDisablePassiveModeForServersThatRequireActiveDataConnections,
    RemoteTests.FtpClientListsNamesWhenDetailedListFormatIsUnsupported,
    BackupServiceTests.BackupDownloadsExistingAndLogsNewFile,
    BackupServiceTests.BackupLogFileUsesBackupFolderNameAndRecordsTimestamp,
    BackupServiceTests.BackupLogWriterHonorsSelectedFields,
    BackupServiceTests.BackupLogWriterFormatsLastModifiedAsBeijingTimeInMarkdown,
    BackupServiceTests.BackupServiceWritesSelectedFullPathFields,
    BackupServiceTests.InvalidFolderTemplateThrowsBeforeCreatingOutsideBackupRoot,
    BackupServiceTests.RootedFolderTemplateThrowsBeforeCreatingOutsideBackupRoot,
    BackupServiceTests.AlreadyCanceledBackupDoesNotCreateBackupFolder,
    BackupServiceTests.BackupLogWriterCanceledBeforeReplacementPreservesExistingLog,
    BackupServiceTests.CanceledBackupDoesNotCreatePartialFile,
    CheckServiceTests.CheckIgnoresDraftFilesThatAreNotInPathList,
    CheckServiceTests.CheckTreatsDuplicatePathsCaseInsensitively,
    UploadServiceTests.UploadCopiesLocalFileToDraft,
    UploadServiceTests.UploadLogsOverwriteWhenDraftFileAlreadyExists,
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
