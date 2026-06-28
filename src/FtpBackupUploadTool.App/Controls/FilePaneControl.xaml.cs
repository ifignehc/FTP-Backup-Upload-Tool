using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FtpBackupUploadTool.App.ViewModels;
using FtpBackupUploadTool.Core.Models;

namespace FtpBackupUploadTool.App.Controls;

public partial class FilePaneControl : UserControl
{
    private Point? dragStartPoint;

    public FilePaneControl()
    {
        InitializeComponent();
    }

    public event EventHandler<IReadOnlyList<FileEntry>>? CopyRequested;

    public event EventHandler<IReadOnlyList<FileEntry>>? PasteRequested;

    public event EventHandler<IReadOnlyList<FileEntry>>? DeleteRequested;

    public event EventHandler<IReadOnlyList<FileEntry>>? FilesDropped;

    private bool IsPaneReadOnly => DataContext is FilePaneViewModel viewModel && viewModel.IsReadOnly;

    private IReadOnlyList<FileEntry> SelectedFiles =>
        fileListView.SelectedItems.OfType<FileEntry>().ToArray();

    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        copyMenuItem.IsEnabled = SelectedFiles.Count > 0;
        pasteMenuItem.IsEnabled = !IsPaneReadOnly;
        deleteMenuItem.IsEnabled = !IsPaneReadOnly && SelectedFiles.Count > 0;
    }

    private void OnCopyClicked(object sender, RoutedEventArgs e)
    {
        var files = SelectedFiles;
        if (files.Count == 0)
        {
            return;
        }

        CopyRequested?.Invoke(this, files);
    }

    private void OnPasteClicked(object sender, RoutedEventArgs e)
    {
        if (IsPaneReadOnly)
        {
            ShowReadOnlyWarning();
            return;
        }

        PasteRequested?.Invoke(this, SelectedFiles);
    }

    private void OnDeleteClicked(object sender, RoutedEventArgs e)
    {
        if (IsPaneReadOnly)
        {
            ShowReadOnlyWarning();
            return;
        }

        var files = SelectedFiles;
        if (files.Count == 0)
        {
            return;
        }

        DeleteRequested?.Invoke(this, files);
    }

    private void OnListViewPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        dragStartPoint = e.GetPosition(fileListView);
    }

    private void OnListViewPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        dragStartPoint = null;
    }

    private void OnListViewPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            dragStartPoint = null;
            return;
        }

        if (dragStartPoint is not { } startPoint)
        {
            return;
        }

        var currentPoint = e.GetPosition(fileListView);
        if (Math.Abs(currentPoint.X - startPoint.X) <= SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPoint.Y - startPoint.Y) <= SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var files = SelectedFiles;
        if (files.Count == 0)
        {
            return;
        }

        DragDrop.DoDragDrop(fileListView, files.ToArray(), DragDropEffects.Copy);
        dragStartPoint = null;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!TryGetDroppedFiles(e, out var files) || files.Count == 0)
        {
            return;
        }

        if (IsPaneReadOnly)
        {
            e.Effects = DragDropEffects.None;
            ShowReadOnlyWarning();
            return;
        }

        e.Effects = DragDropEffects.Copy;
        FilesDropped?.Invoke(this, files);
    }

    private static bool TryGetDroppedFiles(DragEventArgs e, out IReadOnlyList<FileEntry> files)
    {
        if (e.Data.GetDataPresent(typeof(FileEntry[])) &&
            e.Data.GetData(typeof(FileEntry[])) is FileEntry[] fileArray)
        {
            files = fileArray;
            return true;
        }

        if (e.Data.GetDataPresent(typeof(IReadOnlyList<FileEntry>)) &&
            e.Data.GetData(typeof(IReadOnlyList<FileEntry>)) is IReadOnlyList<FileEntry> fileList)
        {
            files = fileList;
            return true;
        }

        files = Array.Empty<FileEntry>();
        return false;
    }

    private static void ShowReadOnlyWarning()
    {
        MessageBox.Show("生产服务器面板为只读，不能粘贴、删除或拖放文件。", "只读面板", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
