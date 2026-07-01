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
    SettingsViewModelTests.BuildsAndLoadsCheckLogSettings,
    SettingsViewModelTests.NewProcessResetsCheckLogSettingsToDefaults,
    SettingsViewModelTests.SettingsWindowDoesNotExposeLocalRootBinding,
    MainWindowTests.MainWindowTitleIsFtpBuTool,
    MainWindowTests.InitialRemoteRefreshTimeoutAllowsSlowCompanyFtpListing,
    MainWindowTests.WindowShortcutsUseCtrlCopyPasteWithoutF5Copy,
    MainWindowTests.ActiveFilePaneUsesBlueLeftFrameWithoutTextLabel,
    MainWindowTests.LocalCopyTargetMessagesNameBackupPaneSeparately,
    PublishScriptTests.PublishScriptNamesPortableExeFtpBuTool,
    PublishScriptTests.PublishScriptSupportsBuildVersionProperties,
    AppProjectTests.AppProjectEmbedsWindowIconResource,
    LogForegroundConverterTests.WarningAndErrorLogsUseDistinctForegroundColors,
    FileEntryTests.FileEntryFormatsUtcLastModifiedAsBeijingTime,
    FileEntryTests.FileEntryDoesNotAddEightHoursToAlreadyBeijingLastModified,
    FileEntryTests.FileEntryFormatsMissingLastModifiedAsBlank,
    FilePaneViewModelTests.SortByNameTogglesDirection,
    FilePaneViewModelTests.SortBySizeOrdersFilesByNumericSize,
    FilePaneViewModelTests.SortByLastModifiedOrdersFilesByTimestamp,
    FilePaneViewModelTests.CountsVisibleDirectoriesAndFiles,
    MainViewModelTests.StartsWithEmptyPathList,
    MainViewModelTests.PathListCountUpdatesWhenTextChanges,
    MainViewModelTests.ConsolidatePathListRemovesDuplicatesAndBlankLines,
    MainViewModelTests.WorkflowCommandsRequireNonBlankPathList,
    MainViewModelTests.PathListTextChangeRaisesWorkflowCommandCanExecuteChanged,
    MainViewModelTests.LoadProcessInitializesBackupPaneFromBackupDirectory,
    MainViewModelTests.RefreshFilePanesListsBackupPaneDirectory,
    MainViewModelTests.RefreshFilePaneOnlyRefreshesRequestedPane,
    MainViewModelTests.UploadUsesCurrentLocalPaneDirectoryAsLocalRoot,
    MainViewModelTests.CheckUsesCurrentLocalPaneDirectoryAsLocalRoot,
    MainViewModelTests.CheckWritesConfiguredMarkdownLog,
    MainViewModelTests.UploadStillUsesLocalPaneWhenBackupPanePointsElsewhere,
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
    RemoteTests.FtpClientUsesFileMetadataWhenNlstAcceptsFilePaths,
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
    CheckServiceTests.CheckReportsPathListStatusesAndLocalFilesMissingFromPathList,
    CheckServiceTests.CheckDoesNotReportDraftFilesThatAreNotInPathList,
    CheckServiceTests.CheckTreatsDuplicatePathsCaseInsensitively,
    CheckServiceTests.CheckLogWriterWritesGroupedMarkdownWithUpdatedFileDates,
    CheckServiceTests.CheckLogWriterCreatesUniqueMarkdownForEachRun,
    CheckServiceTests.CheckLogWriterRendersFileNameTemplateWithBeijingTime,
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
