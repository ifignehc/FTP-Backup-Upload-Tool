using System.Collections.ObjectModel;
using FtpBackupUploadTool.Core.Models;

namespace FtpBackupUploadTool.App.ViewModels;

public sealed class FilePaneViewModel
{
    private string currentPath = "/";

    public FilePaneViewModel(string title, bool isReadOnly, IEnumerable<FileEntry> files)
    {
        Title = title;
        IsReadOnly = isReadOnly;
        Files = new ObservableCollection<FileEntry>(files);
    }

    public string Title { get; }

    public string CurrentPath
    {
        get => currentPath;
        set => currentPath = string.IsNullOrWhiteSpace(value) ? "/" : value;
    }

    public bool IsReadOnly { get; }

    public ObservableCollection<FileEntry> Files { get; }

    public void ReplaceFiles(IEnumerable<FileEntry> files)
    {
        Files.Clear();

        foreach (var file in files.OrderBy(file => file.Path.Value, StringComparer.OrdinalIgnoreCase))
        {
            Files.Add(file);
        }
    }
}
