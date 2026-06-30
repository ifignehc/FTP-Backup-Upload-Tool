using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using FtpBackupUploadTool.Core.Models;

namespace FtpBackupUploadTool.App.ViewModels;

public sealed class FilePaneViewModel : INotifyPropertyChanged
{
    private string currentPath = "/";

    public FilePaneViewModel(
        string title,
        bool isReadOnly,
        IEnumerable<FileEntry> files,
        bool usesAbsolutePaths = false)
    {
        Title = title;
        IsReadOnly = isReadOnly;
        UsesAbsolutePaths = usesAbsolutePaths;
        currentPath = usesAbsolutePaths ? NormalizePath(null, true) : "/";
        Files = new ObservableCollection<FileEntry>(files);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; }

    public string CurrentPath
    {
        get => currentPath;
        set
        {
            var normalized = NormalizePath(value, UsesAbsolutePaths);
            if (currentPath == normalized)
            {
                return;
            }

            currentPath = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentPath)));
        }
    }

    public bool IsReadOnly { get; }

    public bool UsesAbsolutePaths { get; }

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

    public string GetParentPath()
    {
        if (UsesAbsolutePaths)
        {
            var directory = new DirectoryInfo(CurrentPath);
            return directory.Parent?.FullName ?? directory.Root.FullName;
        }

        var current = NormalizePath(CurrentPath).Trim('/');
        var index = current.LastIndexOf('/');
        return index < 0 ? "/" : current[..index];
    }

    public string GetChildPath(string relativePath)
    {
        if (UsesAbsolutePaths)
        {
            return NormalizePath(Path.Combine(CurrentPath, relativePath.Replace('/', Path.DirectorySeparatorChar)), true);
        }

        return NormalizePath(relativePath);
    }

    public void NormalizeCurrentPath()
    {
        CurrentPath = NormalizePath(CurrentPath, UsesAbsolutePaths);
    }

    public static string NormalizePath(string? value) => NormalizePath(value, false);

    public static string NormalizePath(string? value, bool usesAbsolutePaths)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return usesAbsolutePaths ? GetDefaultLocalDirectory() : "/";
        }

        if (usesAbsolutePaths)
        {
            var expanded = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
            if (expanded.Length >= 4 && expanded[0] == '/' && char.IsLetter(expanded[1]) && expanded[2] == ':')
            {
                expanded = expanded[1..];
            }

            return Path.GetFullPath(expanded);
        }

        var normalized = value.Trim().Replace('\\', '/').Trim('/');
        return normalized.Length == 0 ? "/" : "/" + normalized;
    }

    private static string GetDefaultLocalDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(userProfile) || !Directory.Exists(userProfile)
            ? Directory.GetCurrentDirectory()
            : userProfile;
    }
}
