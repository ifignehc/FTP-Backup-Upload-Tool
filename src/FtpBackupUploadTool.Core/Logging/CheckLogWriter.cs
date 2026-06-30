using System.Globalization;
using System.Text;
using FtpBackupUploadTool.Core.Formatting;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;

namespace FtpBackupUploadTool.Core.Logging;

public sealed record CheckLogRow(
    RelativePath RelativePath,
    OperationLogLevel Level,
    DateTimeOffset? FileDate,
    string Message);

public sealed class CheckLogWriter
{
    public async Task<string> WriteAsync(
        string logDirectory,
        string fileNameTemplate,
        IReadOnlyList<CheckLogRow> rows,
        CancellationToken cancellationToken,
        DateTimeOffset? checkTime = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var timestamp = checkTime ?? new DateTimeOffset(DateTime.UtcNow, TimeSpan.Zero);
        var renderedName = RenderFileName(fileNameTemplate, timestamp);
        var fileName = EnsureMarkdownExtension(renderedName);
        ValidateLogFileName(fileName);

        var directory = Path.GetFullPath(Environment.ExpandEnvironmentVariables(logDirectory));
        Directory.CreateDirectory(directory);
        var logPath = GetUniqueLogPath(directory, fileName);
        var title = Path.GetFileNameWithoutExtension(fileName);
        var lines = BuildDocument(title, rows, timestamp);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(logPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllLinesAsync(tempPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, logPath);
            return logPath;
        }
        catch
        {
            TryDeleteTempFile(tempPath);
            throw;
        }
    }

    private static List<string> BuildDocument(string title, IReadOnlyList<CheckLogRow> rows, DateTimeOffset checkTime)
    {
        var updated = rows.Where(row => row.Level == OperationLogLevel.Normal).ToArray();
        var warnings = rows.Where(row => row.Level == OperationLogLevel.Warning).ToArray();
        var errors = rows.Where(row => row.Level == OperationLogLevel.Error).ToArray();
        var lines = new List<string>
        {
            $"# {Escape(title)}",
            $"- CheckTime: {TimeDisplayFormatter.FormatBeijingTime(checkTime)}",
            $"- UpdatedFiles: {updated.Length}",
            $"- Warnings: {warnings.Length}",
            $"- Errors: {errors.Length}",
            string.Empty
        };

        AddSection(lines, "Updated Files", updated, includeFileDate: true);
        AddSection(lines, "Warnings", warnings, includeFileDate: false);
        AddSection(lines, "Errors", errors, includeFileDate: false);

        return lines;
    }

    private static void AddSection(List<string> lines, string title, IReadOnlyList<CheckLogRow> rows, bool includeFileDate)
    {
        lines.Add($"## {title}");

        if (rows.Count == 0)
        {
            lines.Add("- None");
            lines.Add(string.Empty);
            return;
        }

        for (var index = 0; index < rows.Count; index++)
        {
            if (index > 0)
            {
                lines.Add(string.Empty);
            }

            var row = rows[index];
            lines.Add($"### {Escape(row.RelativePath.Value)}");
            lines.Add($"- Path: {Escape(row.RelativePath.Value)}");
            if (includeFileDate)
            {
                lines.Add($"- FileDate: {TimeDisplayFormatter.FormatBeijingTime(row.FileDate)}");
            }

            lines.Add($"- Message: {Escape(row.Message)}");
        }

        lines.Add(string.Empty);
    }

    private static string RenderFileName(string template, DateTimeOffset timestamp)
    {
        var beijingTime = DateTime.SpecifyKind(timestamp.DateTime, DateTimeKind.Utc).AddHours(8);
        return template
            .Replace("{yyyy}", beijingTime.ToString("yyyy", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{MM}", beijingTime.ToString("MM", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{dd}", beijingTime.ToString("dd", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{HH}", beijingTime.ToString("HH", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{mm}", beijingTime.ToString("mm", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{ss}", beijingTime.ToString("ss", CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    private static string EnsureMarkdownExtension(string fileName)
    {
        return fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : $"{fileName}.md";
    }

    private static void ValidateLogFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName is "." or ".."
            || Path.IsPathFullyQualified(fileName)
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || fileName.Contains(Path.DirectorySeparatorChar)
            || fileName.Contains(Path.AltDirectorySeparatorChar)
            || fileName.Contains('/')
            || fileName.Contains('\\')
            || Path.GetFileName(fileName) != fileName)
        {
            throw new ArgumentException("Rendered check log template must be a single safe Markdown file name.", nameof(fileName));
        }
    }

    private static string GetUniqueLogPath(string directory, string fileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var candidate = Path.Combine(directory, fileName);
        var index = 2;

        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{baseName}-{index}{extension}");
            index++;
        }

        return candidate;
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
