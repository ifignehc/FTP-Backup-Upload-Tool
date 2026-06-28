using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;

namespace FtpBackupUploadTool.Core.Remote;

public interface IRemoteFileClient
{
    Task<IReadOnlyList<FileEntry>> ListRecursiveAsync(CancellationToken cancellationToken);
    Task<bool> FileExistsAsync(RelativePath path, CancellationToken cancellationToken);
    Task<bool> DirectoryExistsAsync(RelativePath path, CancellationToken cancellationToken);
    Task<FileEntry?> GetFileEntryAsync(RelativePath path, CancellationToken cancellationToken);
    Task DownloadAsync(RelativePath path, Stream destination, CancellationToken cancellationToken);
    Task UploadAsync(RelativePath path, Stream source, CancellationToken cancellationToken);
    Task DeleteFileAsync(RelativePath path, CancellationToken cancellationToken);
}
