using FtpBackupUploadTool.Core.Paths;

namespace FtpBackupUploadTool.Core.Models;

public enum OperationLogLevel
{
    Normal,
    Warning,
    Error
}

public sealed record OperationLogEntry(
    DateTimeOffset Timestamp,
    OperationLogLevel Level,
    string Operation,
    RelativePath? Path,
    string Message,
    string? Error = null);
