using FtpBackupUploadTool.Core.Services;

namespace FtpBackupUploadTool.App.Runtime;

public sealed record WorkflowServices(
    BackupService BackupService,
    UploadService UploadService,
    CheckService CheckService);
