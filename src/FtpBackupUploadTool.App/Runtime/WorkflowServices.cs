using FtpBackupUploadTool.Core.Services;
using FtpBackupUploadTool.Core.Remote;

namespace FtpBackupUploadTool.App.Runtime;

public sealed record WorkflowServices(
    BackupService BackupService,
    UploadService UploadService,
    CheckService CheckService,
    IRemoteFileClient ProductionClient,
    IRemoteFileClient DraftClient);
