namespace FtpBackupUploadTool.Core.Models;

public sealed record ServerConfig(
    string Host,
    int Port,
    string UserName,
    string EncryptedPassword,
    string RootPath,
    bool UsePassive = true);

public sealed record BackupConfig(string BackupDirectory, string FolderNameTemplate, LogFieldOptions LogFields);

public sealed record CheckLogConfig(string LogDirectory, string FileNameTemplate)
{
    public const string DefaultLogDirectory = @"%USERPROFILE%\Desktop";
    public const string DefaultFileNameTemplate = "{yyyy}{MM}{dd}_{HH}{mm}{ss}_Check";

    public static CheckLogConfig Default { get; } = new(DefaultLogDirectory, DefaultFileNameTemplate);
}

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
    BackupConfig Backup,
    CheckLogConfig? CheckLog = null)
{
    public CheckLogConfig CheckLog { get; init; } = CheckLog ?? CheckLogConfig.Default;
}
