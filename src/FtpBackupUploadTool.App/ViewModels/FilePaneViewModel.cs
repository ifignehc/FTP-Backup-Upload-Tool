using System.Collections.ObjectModel;
using System.ComponentModel;
using FtpBackupUploadTool.Core.Models;

namespace FtpBackupUploadTool.App.ViewModels;

public sealed class FilePaneViewModel : INotifyPropertyChanged
{
    private string currentPath = "/";

    public FilePaneViewModel(string title, bool isReadOnly, IEnumerable<FileEntry> files)
    {
        Title = title;
        IsReadOnly = isReadOnly;
        Files = new ObservableCollection<FileEntry>(files);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; }

    public string CurrentPath
    {
        get => currentPath;
        set
        {
            var normalized = NormalizePath(value);
            if (currentPath == normalized)
            {
                return;
            }

            currentPath = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentPath)));
        }
    }

    public bool IsReadOnly { get; }

    public ObservableCollection<FileEntry> Files { get; }

    public void ReplaceFiles(IEnumerable<FileEntry> files)
    {
        Files.Clear();

        foreach (var file in files
                     .OrderByDescending(file => file.IsDirectory)
                     .ThenBy(file => file.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            Files.Add(file);
        }
    }

    public static string NormalizePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "/";
        }

        var normalized = value.Trim().Replace('\\', '/').Trim('/');
        return normalized.Length == 0 ? "/" : "/" + normalized;
    }
}
