using System.Text.Json;
using FtpBackupUploadTool.Core.Models;

namespace FtpBackupUploadTool.Core.Config;

public sealed record AppConfig(ProcessConfig[] Processes);

public sealed class AppConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string filePath;

    public AppConfigStore(string filePath)
    {
        this.filePath = filePath;
    }

    public async Task<AppConfig> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return new AppConfig(Array.Empty<ProcessConfig>());
        }

        await using var stream = File.OpenRead(filePath);
        var config = await JsonSerializer.DeserializeAsync<AppConfig>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);

        return config ?? new AppConfig(Array.Empty<ProcessConfig>());
    }

    public async Task SaveAsync(AppConfig config, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, config, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public static string GetDefaultConfigPath()
    {
        var executableDirectory = AppContext.BaseDirectory;
        var localConfigPath = Path.Combine(executableDirectory, "config", "appsettings.json");

        try
        {
            EnsureDirectoryWritable(Path.GetDirectoryName(localConfigPath));
            return localConfigPath;
        }
        catch (UnauthorizedAccessException)
        {
            return GetAppDataConfigPath();
        }
        catch (IOException)
        {
            return GetAppDataConfigPath();
        }
    }

    private static void EnsureDirectoryWritable(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new IOException("Config directory path is empty.");
        }

        Directory.CreateDirectory(directory);
        var probePath = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}.tmp");

        using (new FileStream(probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 1, FileOptions.DeleteOnClose))
        {
        }
    }

    private static string GetAppDataConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "FtpBackupUploadTool", "appsettings.json");
    }
}
