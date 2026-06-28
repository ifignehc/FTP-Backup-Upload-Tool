namespace FtpBackupUploadTool.Core.Models;

public sealed record ServerConfig(string Host, int Port, string UserName, string EncryptedPassword, string RootPath);

public sealed record BackupConfig(string BackupDirectory, string FolderNameTemplate, LogFieldOptions LogFields);

[Flags]
public enum LogFieldOptions
{
    None = 0,
    RelativePath = 1,
    ProductionFullPath = 2,
    DraftFullPath = 4,
    LocalFullPath = 8,
    FileSize = 16,
    LastModified = 32,
    Result = 64,
    ErrorMessage = 128,
    Note = 256,
    All = RelativePath | ProductionFullPath | DraftFullPath | LocalFullPath | FileSize | LastModified | Result | ErrorMessage | Note
}

public sealed record ProcessConfig(
    string Name,
    ServerConfig ProductionServer,
    ServerConfig DraftServer,
    string LocalRootPath,
    string DefaultPathListFile,
    BackupConfig Backup);
