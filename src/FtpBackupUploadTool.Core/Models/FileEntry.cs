using FtpBackupUploadTool.Core.Formatting;
using FtpBackupUploadTool.Core.Paths;

namespace FtpBackupUploadTool.Core.Models;

public sealed record FileEntry(RelativePath Path, bool IsDirectory, long Size, DateTimeOffset? LastModified)
{
    public string DisplayName
    {
        get
        {
            var normalized = Path.Value.TrimEnd('/').Replace('\\', '/');
            var index = normalized.LastIndexOf('/');
            var name = index < 0 ? normalized : normalized[(index + 1)..];
            return IsDirectory ? name + "/" : name;
        }
    }

    public string SizeDisplay => IsDirectory ? string.Empty : Size.ToString("N0");

    public string LastModifiedDisplay => TimeDisplayFormatter.FormatBeijingTime(LastModified);
}
