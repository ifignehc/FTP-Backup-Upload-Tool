using FtpBackupUploadTool.Core.Paths;

namespace FtpBackupUploadTool.Core.Models;

public sealed record FileEntry(RelativePath Path, bool IsDirectory, long Size, DateTimeOffset? LastModified);
