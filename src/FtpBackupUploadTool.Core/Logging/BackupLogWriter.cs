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
    private static readonly TimeSpan BeijingOffset = TimeSpan.FromHours(8);

    public async Task WriteAsync(
        string logPath,
        IReadOnlyList<BackupLogRow> rows,
        LogFieldOptions fields,
        CancellationToken cancellationToken,
        string? title = null,
        DateTimeOffset? backupTime = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var lines = BuildDocument(rows, fields, title, backupTime);

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

    private static List<string> BuildDocument(
        IReadOnlyList<BackupLogRow> rows,
        LogFieldOptions fields,
        string? title,
        DateTimeOffset? backupTime)
    {
        var lines = new List<string> { $"# {Escape(string.IsNullOrWhiteSpace(title) ? "Backup Log" : title)}" };

        if (backupTime is not null)
        {
            lines.Add($"- BackupTime: {FormatBeijingTime(backupTime)}");
        }

        for (var index = 0; index < rows.Count; index++)
        {
            if (index > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add($"## Entry {index + 1}");
            lines.AddRange(BuildEntryLines(rows[index], fields));
        }

        return lines;
    }

    private static IEnumerable<string> BuildEntryLines(BackupLogRow row, LogFieldOptions fields)
    {
        foreach (var field in SelectedFields(fields))
        {
            yield return $"- {field}: {Escape(field switch
            {
                "RelativePath" => row.RelativePath.Value,
                "ProductionFullPath" => row.ProductionFullPath,
                "DraftFullPath" => row.DraftFullPath,
                "LocalFullPath" => row.LocalFullPath,
                "FileSize" => row.FileSize?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                "LastModified" => FormatBeijingTime(row.LastModified),
                "Result" => row.Result,
                "ErrorMessage" => row.ErrorMessage,
                "Note" => row.Note,
                _ => string.Empty
            })}";
        }
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

    private static string FormatBeijingTime(DateTimeOffset? value)
    {
        return value is null
            ? string.Empty
            : DateTime.SpecifyKind(value.Value.DateTime, DateTimeKind.Utc)
                .Add(BeijingOffset)
                .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static string Escape(string value)
    {
        return value
            .Replace("\r\n", "<br>", StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal)
            .Replace("\r", "<br>", StringComparison.Ordinal);
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
