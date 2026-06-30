using FtpBackupUploadTool.Core.Logging;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Remote;
using FtpBackupUploadTool.Core.Security;
using FtpBackupUploadTool.Core.Services;

namespace FtpBackupUploadTool.App.Runtime;

public sealed class ProcessRuntimeFactory
{
    private readonly IPasswordProtector passwordProtector;

    public ProcessRuntimeFactory(IPasswordProtector passwordProtector)
    {
        this.passwordProtector = passwordProtector ?? throw new ArgumentNullException(nameof(passwordProtector));
    }

    public WorkflowServices Create(ProcessConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var productionPassword = ResolvePassword(config.ProductionServer.EncryptedPassword);
        var draftPassword = ResolvePassword(config.DraftServer.EncryptedPassword);

        var production = new FtpRemoteFileClient(
            config.ProductionServer.Host,
            config.ProductionServer.Port,
            config.ProductionServer.RootPath,
            config.ProductionServer.UserName,
            productionPassword,
            config.ProductionServer.UsePassive);
        var draft = new FtpRemoteFileClient(
            config.DraftServer.Host,
            config.DraftServer.Port,
            config.DraftServer.RootPath,
            config.DraftServer.UserName,
            draftPassword,
            config.DraftServer.UsePassive);

        return new WorkflowServices(
            new BackupService(production, new BackupLogWriter()),
            new UploadService(draft),
            new CheckService(production, draft),
            production,
            draft);
    }

    private string ResolvePassword(string encryptedPassword)
    {
        return string.IsNullOrWhiteSpace(encryptedPassword)
            ? string.Empty
            : passwordProtector.Unprotect(encryptedPassword);
    }
}
