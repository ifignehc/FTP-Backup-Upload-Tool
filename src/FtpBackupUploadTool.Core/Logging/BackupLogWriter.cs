using System.Globalization;
using System.Text;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;

namespace FtpBackupUploadTool.Core.Logging;

public sealed record BackupLogRow(
    RelativePath RelativePath,
    string ProductionFullPath,
    string DraftFullPath,
    string LocalFullPath,
    long? FileSize,
    DateTimeOffset? LastModified,
    string Result,
    string ErrorMessage,
    string Note);

public sealed class BackupLogWriter
{
    public async Task WriteAsync(
        string logPath,
        IReadOnlyList<BackupLogRow> rows,
        LogFieldOptions fields,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var lines = new List<string> { BuildHeader(fields) };
        lines.AddRange(rows.Select(row => BuildLine(row, fields)));

        var directory = Path.GetDirectoryName(logPath) ?? ".";
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(logPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllLinesAsync(tempPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(logPath))
            {
                File.Replace(tempPath, logPath, null);
            }
            else
            {
                File.Move(tempPath, logPath);
            }
        }
        catch
        {
            TryDeleteTempFile(tempPath);
            throw;
        }
    }

    private static string BuildHeader(LogFieldOptions fields)
    {
        return string.Join(',', SelectedFields(fields));
    }

    private static string BuildLine(BackupLogRow row, LogFieldOptions fields)
    {
        var values = new List<string>();
        foreach (var field in SelectedFields(fields))
        {
            values.Add(Escape(field switch
            {
                "RelativePath" => row.RelativePath.Value,
                "ProductionFullPath" => row.ProductionFullPath,
                "DraftFullPath" => row.DraftFullPath,
                "LocalFullPath" => row.LocalFullPath,
                "FileSize" => row.FileSize?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                "LastModified" => row.LastModified?.ToString("u", CultureInfo.InvariantCulture) ?? string.Empty,
                "Result" => row.Result,
                "ErrorMessage" => row.ErrorMessage,
                "Note" => row.Note,
                _ => string.Empty
            }));
        }

        return string.Join(',', values);
    }

    private static IReadOnlyList<string> SelectedFields(LogFieldOptions fields)
    {
        var selected = new List<string>();
        if (fields.HasFlag(LogFieldOptions.RelativePath)) selected.Add("RelativePath");
        if (fields.HasFlag(LogFieldOptions.ProductionFullPath)) selected.Add("ProductionFullPath");
        if (fields.HasFlag(LogFieldOptions.DraftFullPath)) selected.Add("DraftFullPath");
        if (fields.HasFlag(LogFieldOptions.LocalFullPath)) selected.Add("LocalFullPath");
        if (fields.HasFlag(LogFieldOptions.FileSize)) selected.Add("FileSize");
        if (fields.HasFlag(LogFieldOptions.LastModified)) selected.Add("LastModified");
        if (fields.HasFlag(LogFieldOptions.Result)) selected.Add("Result");
        if (fields.HasFlag(LogFieldOptions.ErrorMessage)) selected.Add("ErrorMessage");
        if (fields.HasFlag(LogFieldOptions.Note)) selected.Add("Note");
        return selected;
    }

    private static string Escape(string value)
    {
        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }

    private static void TryDeleteTempFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
