using System.Collections.ObjectModel;
using FtpBackupUploadTool.Core.Models;

namespace FtpBackupUploadTool.App.ViewModels;

public sealed class FilePaneViewModel
{
    public FilePaneViewModel(string title, bool isReadOnly, IEnumerable<FileEntry> files)
    {
        Title = title;
        IsReadOnly = isReadOnly;
        Files = new ObservableCollection<FileEntry>(files);
    }

    public string Title { get; }

    public string CurrentPath { get; } = "/";

    public bool IsReadOnly { get; }

    public ObservableCollection<FileEntry> Files { get; }
}
