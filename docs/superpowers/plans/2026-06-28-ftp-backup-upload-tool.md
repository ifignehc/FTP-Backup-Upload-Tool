# FTP Backup Upload Tool Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows portable WPF `.exe` for FTP-based production backup, draft-server upload, status check, configuration, encrypted password storage, and operator logs.

**Architecture:** Create a small Windows-targeted .NET solution with a UI app, a testable core library, and a no-NuGet console test runner. Core services own path parsing, config, backup, upload, and check rules behind remote-file abstractions; WPF binds to those services and keeps production FTP read-only.

**Tech Stack:** .NET 8, WPF, C#, Windows DPAPI via `System.Security.Cryptography.ProtectedData`, standard-library FTP via `FtpWebRequest`, custom console tests to avoid NuGet dependency during the first build.

---

## Scope And Prerequisites

The current machine has .NET runtimes but no .NET SDK. The first implementation task must install or make available the .NET 8 SDK before scaffolding.

The implementation follows the approved spec:

- `docs/superpowers/specs/2026-06-28-ftp-backup-upload-tool-design.md`
- `mockups/main-design-wireframe.html`
- `mockups/settings-design-wireframe.html`

## File Structure

Create these project files:

- `FtpBackupUploadTool.sln` - solution file.
- `src/FtpBackupUploadTool.Core/FtpBackupUploadTool.Core.csproj` - testable domain and application logic.
- `src/FtpBackupUploadTool.App/FtpBackupUploadTool.App.csproj` - WPF executable.
- `tests/FtpBackupUploadTool.Tests/FtpBackupUploadTool.Tests.csproj` - console test runner.

Core files:

- `src/FtpBackupUploadTool.Core/Models/ProcessConfig.cs` - process, server, backup, and log-field settings.
- `src/FtpBackupUploadTool.Core/Models/FileEntry.cs` - file metadata used by panes and services.
- `src/FtpBackupUploadTool.Core/Models/OperationLogEntry.cs` - UI and backup log record.
- `src/FtpBackupUploadTool.Core/Paths/RelativePath.cs` - path normalization and validation.
- `src/FtpBackupUploadTool.Core/Paths/PathListParser.cs` - parse pasted, TXT, and CSV path lists.
- `src/FtpBackupUploadTool.Core/Remote/IRemoteFileClient.cs` - production/draft remote interface.
- `src/FtpBackupUploadTool.Core/Remote/LocalMirrorRemoteClient.cs` - test and manual-dev remote backed by local folders.
- `src/FtpBackupUploadTool.Core/Remote/FtpRemoteFileClient.cs` - FTP implementation.
- `src/FtpBackupUploadTool.Core/Security/IPasswordProtector.cs` - password encryption contract.
- `src/FtpBackupUploadTool.Core/Security/DpapiPasswordProtector.cs` - Windows current-user password encryption.
- `src/FtpBackupUploadTool.Core/Config/AppConfigStore.cs` - config path selection, JSON load/save, encrypted passwords.
- `src/FtpBackupUploadTool.Core/Logging/BackupLogWriter.cs` - backup-folder log file writer.
- `src/FtpBackupUploadTool.Core/Services/BackupService.cs` - production backup workflow.
- `src/FtpBackupUploadTool.Core/Services/UploadService.cs` - local-to-draft upload workflow.
- `src/FtpBackupUploadTool.Core/Services/CheckService.cs` - Normal, Warning, Error status rules.

App files:

- `src/FtpBackupUploadTool.App/App.xaml` - WPF application entry.
- `src/FtpBackupUploadTool.App/App.xaml.cs` - startup and service wiring.
- `src/FtpBackupUploadTool.App/MainWindow.xaml` - main three-pane layout.
- `src/FtpBackupUploadTool.App/MainWindow.xaml.cs` - main window event bridge.
- `src/FtpBackupUploadTool.App/ViewModels/MainViewModel.cs` - main screen state and commands.
- `src/FtpBackupUploadTool.App/ViewModels/FilePaneViewModel.cs` - one file pane state.
- `src/FtpBackupUploadTool.App/ViewModels/SettingsViewModel.cs` - settings state and save command.
- `src/FtpBackupUploadTool.App/Views/SettingsWindow.xaml` - settings dialog.
- `src/FtpBackupUploadTool.App/Views/SettingsWindow.xaml.cs` - settings dialog event bridge.
- `src/FtpBackupUploadTool.App/Controls/FilePaneControl.xaml` - reusable file pane.
- `src/FtpBackupUploadTool.App/Controls/FilePaneControl.xaml.cs` - copy, paste, delete, drag/drop event bridge.
- `src/FtpBackupUploadTool.App/Commands/RelayCommand.cs` - command helper.

Test files:

- `tests/FtpBackupUploadTool.Tests/Program.cs` - runs all tests and returns non-zero on failure.
- `tests/FtpBackupUploadTool.Tests/TestAssert.cs` - minimal assertion helpers.
- `tests/FtpBackupUploadTool.Tests/PathTests.cs`
- `tests/FtpBackupUploadTool.Tests/ConfigTests.cs`
- `tests/FtpBackupUploadTool.Tests/BackupServiceTests.cs`
- `tests/FtpBackupUploadTool.Tests/UploadServiceTests.cs`
- `tests/FtpBackupUploadTool.Tests/CheckServiceTests.cs`

---

### Task 1: Verify SDK And Scaffold Solution

**Files:**
- Create: `FtpBackupUploadTool.sln`
- Create: `src/FtpBackupUploadTool.Core/FtpBackupUploadTool.Core.csproj`
- Create: `src/FtpBackupUploadTool.App/FtpBackupUploadTool.App.csproj`
- Create: `tests/FtpBackupUploadTool.Tests/FtpBackupUploadTool.Tests.csproj`

- [ ] **Step 1: Verify .NET SDK availability**

Run:

```powershell
dotnet --list-sdks
```

Expected if ready:

```text
8.0.
```

If the command prints nothing, install Microsoft .NET 8 SDK x64 for Windows, reopen PowerShell, and rerun the command until an `8.0` SDK line appears.

- [ ] **Step 2: Create the solution and projects**

Run:

```powershell
dotnet new sln -n FtpBackupUploadTool
dotnet new classlib -n FtpBackupUploadTool.Core -o src/FtpBackupUploadTool.Core -f net8.0
dotnet new wpf -n FtpBackupUploadTool.App -o src/FtpBackupUploadTool.App -f net8.0-windows
dotnet new console -n FtpBackupUploadTool.Tests -o tests/FtpBackupUploadTool.Tests -f net8.0
dotnet sln FtpBackupUploadTool.sln add src/FtpBackupUploadTool.Core/FtpBackupUploadTool.Core.csproj
dotnet sln FtpBackupUploadTool.sln add src/FtpBackupUploadTool.App/FtpBackupUploadTool.App.csproj
dotnet sln FtpBackupUploadTool.sln add tests/FtpBackupUploadTool.Tests/FtpBackupUploadTool.Tests.csproj
dotnet add src/FtpBackupUploadTool.App/FtpBackupUploadTool.App.csproj reference src/FtpBackupUploadTool.Core/FtpBackupUploadTool.Core.csproj
dotnet add tests/FtpBackupUploadTool.Tests/FtpBackupUploadTool.Tests.csproj reference src/FtpBackupUploadTool.Core/FtpBackupUploadTool.Core.csproj
```

Expected:

```text
The template "Solution File" was created successfully.
The template "Class Library" was created successfully.
The template "WPF Application" was created successfully.
The template "Console App" was created successfully.
Project reference added.
```

- [ ] **Step 3: Pin project settings**

Replace `src/FtpBackupUploadTool.Core/FtpBackupUploadTool.Core.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <NoWarn>SYSLIB0014</NoWarn>
  </PropertyGroup>
</Project>
```

Replace `src/FtpBackupUploadTool.App/FtpBackupUploadTool.App.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AssemblyName>FtpBackupUploadTool</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\FtpBackupUploadTool.Core\FtpBackupUploadTool.Core.csproj" />
  </ItemGroup>
</Project>
```

Replace `tests/FtpBackupUploadTool.Tests/FtpBackupUploadTool.Tests.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\FtpBackupUploadTool.Core\FtpBackupUploadTool.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Build the empty solution**

Run:

```powershell
dotnet build FtpBackupUploadTool.sln
```

Expected:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

- [ ] **Step 5: Commit scaffold**

Run:

```powershell
git add FtpBackupUploadTool.sln src tests
git commit -m "chore: scaffold WPF FTP backup tool"
```

---

### Task 2: Add Test Runner And Path Parsing

**Files:**
- Create: `tests/FtpBackupUploadTool.Tests/TestAssert.cs`
- Modify: `tests/FtpBackupUploadTool.Tests/Program.cs`
- Create: `tests/FtpBackupUploadTool.Tests/PathTests.cs`
- Create: `src/FtpBackupUploadTool.Core/Paths/RelativePath.cs`
- Create: `src/FtpBackupUploadTool.Core/Paths/PathListParser.cs`

- [ ] **Step 1: Write failing path tests**

Create `tests/FtpBackupUploadTool.Tests/TestAssert.cs`:

```csharp
namespace FtpBackupUploadTool.Tests;

internal static class TestAssert
{
    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message}. Expected: {expected}; Actual: {actual}");
        }
    }

    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
```

Replace `tests/FtpBackupUploadTool.Tests/Program.cs`:

```csharp
using FtpBackupUploadTool.Tests;

var tests = new Action[]
{
    PathTests.NormalizeRelativePaths,
    PathTests.RejectParentTraversal,
    PathTests.ParsePathListText
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {test.Method.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"FAIL {test.Method.Name}: {ex.Message}");
        Console.WriteLine(failures[^1]);
    }
}

return failures.Count == 0 ? 0 : 1;
```

Create `tests/FtpBackupUploadTool.Tests/PathTests.cs`:

```csharp
using FtpBackupUploadTool.Core.Paths;

namespace FtpBackupUploadTool.Tests;

internal static class PathTests
{
    public static void NormalizeRelativePaths()
    {
        var path = RelativePath.Parse(@" /css\site.css ");
        TestAssert.Equal("css/site.css", path.Value, "relative path should be normalized");
    }

    public static void RejectParentTraversal()
    {
        var failed = false;
        try
        {
            RelativePath.Parse("../secret.txt");
        }
        catch (ArgumentException)
        {
            failed = true;
        }

        TestAssert.True(failed, "parent traversal must be rejected");
    }

    public static void ParsePathListText()
    {
        var input = "css/site.css\r\n\r\n images\\\\logo.png \r\n";
        var paths = PathListParser.Parse(input).Select(x => x.Value).ToArray();
        TestAssert.Equal(2, paths.Length, "blank lines should be ignored");
        TestAssert.Equal("css/site.css", paths[0], "first path");
        TestAssert.Equal("images/logo.png", paths[1], "second path");
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet run --project tests/FtpBackupUploadTool.Tests/FtpBackupUploadTool.Tests.csproj
```

Expected:

```text
The type or namespace name 'Paths' does not exist
```

- [ ] **Step 3: Implement path parsing**

Create `src/FtpBackupUploadTool.Core/Paths/RelativePath.cs`:

```csharp
namespace FtpBackupUploadTool.Core.Paths;

public sealed record RelativePath
{
    private RelativePath(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static RelativePath Parse(string input)
    {
        var normalized = input.Trim().Replace('\\', '/');
        while (normalized.StartsWith('/'))
        {
            normalized = normalized[1..];
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("路径不能为空。", nameof(input));
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment == "." || segment == ".."))
        {
            throw new ArgumentException("路径不能包含 . 或 ..。", nameof(input));
        }

        return new RelativePath(string.Join('/', segments));
    }

    public override string ToString() => Value;
}
```

Create `src/FtpBackupUploadTool.Core/Paths/PathListParser.cs`:

```csharp
namespace FtpBackupUploadTool.Core.Paths;

public static class PathListParser
{
    public static IReadOnlyList<RelativePath> Parse(string text)
    {
        return text
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Select(line => line.Trim().Trim(','))
            .Where(line => line.Length > 0)
            .Select(RelativePath.Parse)
            .DistinctBy(path => path.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
```

- [ ] **Step 4: Run tests and verify pass**

Run:

```powershell
dotnet run --project tests/FtpBackupUploadTool.Tests/FtpBackupUploadTool.Tests.csproj
```

Expected:

```text
PASS NormalizeRelativePaths
PASS RejectParentTraversal
PASS ParsePathListText
```

- [ ] **Step 5: Commit path parsing**

Run:

```powershell
git add src/FtpBackupUploadTool.Core/Paths tests/FtpBackupUploadTool.Tests
git commit -m "feat: add relative path parsing"
```

---

### Task 3: Add Domain Models And Remote Abstraction

**Files:**
- Create: `src/FtpBackupUploadTool.Core/Models/ProcessConfig.cs`
- Create: `src/FtpBackupUploadTool.Core/Models/FileEntry.cs`
- Create: `src/FtpBackupUploadTool.Core/Models/OperationLogEntry.cs`
- Create: `src/FtpBackupUploadTool.Core/Remote/IRemoteFileClient.cs`
- Create: `src/FtpBackupUploadTool.Core/Remote/LocalMirrorRemoteClient.cs`

- [ ] **Step 1: Add model and local remote tests**

Create `tests/FtpBackupUploadTool.Tests/RemoteTests.cs`:

```csharp
using FtpBackupUploadTool.Core.Paths;
using FtpBackupUploadTool.Core.Remote;

namespace FtpBackupUploadTool.Tests;

internal static class RemoteTests
{
    public static void LocalMirrorCanWriteAndRead()
    {
        var root = Path.Combine(Path.GetTempPath(), "ftp-tool-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var client = new LocalMirrorRemoteClient(root);
        var relative = RelativePath.Parse("css/site.css");

        using var source = new MemoryStream("body"u8.ToArray());
        client.UploadAsync(relative, source, CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.True(client.FileExistsAsync(relative, CancellationToken.None).GetAwaiter().GetResult(), "uploaded file should exist");
        using var downloaded = new MemoryStream();
        client.DownloadAsync(relative, downloaded, CancellationToken.None).GetAwaiter().GetResult();
        TestAssert.Equal("body", System.Text.Encoding.UTF8.GetString(downloaded.ToArray()), "downloaded content");
    }
}
```

Add `RemoteTests.LocalMirrorCanWriteAndRead` to the `tests` array in `Program.cs`.

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet run --project tests/FtpBackupUploadTool.Tests/FtpBackupUploadTool.Tests.csproj
```

Expected:

```text
The type or namespace name 'Remote' does not exist
```

- [ ] **Step 3: Implement domain and remote files**

Create `src/FtpBackupUploadTool.Core/Models/ProcessConfig.cs`:

```csharp
namespace FtpBackupUploadTool.Core.Models;

public sealed record ServerConfig(string Host, int Port, string UserName, string EncryptedPassword, string RootPath);

public sealed record BackupConfig(string BackupDirectory, string FolderNameTemplate, LogFieldOptions LogFields);

[Flags]
public enum LogFieldOptions
{
    None = 0,
    RelativePath = 1,
    ProductionFullPath = 2,
    DraftFullPath = 4,
    LocalFullPath = 8,
    FileSize = 16,
    LastModified = 32,
    Result = 64,
    ErrorMessage = 128,
    Note = 256,
    All = RelativePath | ProductionFullPath | DraftFullPath | LocalFullPath | FileSize | LastModified | Result | ErrorMessage | Note
}

public sealed record ProcessConfig(
    string Name,
    ServerConfig ProductionServer,
    ServerConfig DraftServer,
    string LocalRootPath,
    string DefaultPathListFile,
    BackupConfig Backup);
```

Create `src/FtpBackupUploadTool.Core/Models/FileEntry.cs`:

```csharp
using FtpBackupUploadTool.Core.Paths;

namespace FtpBackupUploadTool.Core.Models;

public sealed record FileEntry(RelativePath Path, bool IsDirectory, long Size, DateTimeOffset? LastModified);
```

Create `src/FtpBackupUploadTool.Core/Models/OperationLogEntry.cs`:

```csharp
using FtpBackupUploadTool.Core.Paths;

namespace FtpBackupUploadTool.Core.Models;

public enum OperationLogLevel
{
    Normal,
    Warning,
    Error
}

public sealed record OperationLogEntry(
    DateTimeOffset Timestamp,
    OperationLogLevel Level,
    string Operation,
    RelativePath? Path,
    string Message,
    string? Error = null);
```

Create `src/FtpBackupUploadTool.Core/Remote/IRemoteFileClient.cs`:

```csharp
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
```

Create `src/FtpBackupUploadTool.Core/Remote/LocalMirrorRemoteClient.cs`:

```csharp
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;

namespace FtpBackupUploadTool.Core.Remote;

public sealed class LocalMirrorRemoteClient : IRemoteFileClient
{
    private readonly string _root;

    public LocalMirrorRemoteClient(string root)
    {
        _root = root;
        Directory.CreateDirectory(_root);
    }

    public Task<IReadOnlyList<FileEntry>> ListRecursiveAsync(CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)
            .Select(file =>
            {
                var relative = Path.GetRelativePath(_root, file).Replace('\\', '/');
                var info = new FileInfo(file);
                return new FileEntry(RelativePath.Parse(relative), false, info.Length, info.LastWriteTimeUtc);
            })
            .ToArray();

        return Task.FromResult<IReadOnlyList<FileEntry>>(files);
    }

    public Task<bool> FileExistsAsync(RelativePath path, CancellationToken cancellationToken)
        => Task.FromResult(File.Exists(ToFullPath(path)));

    public Task<bool> DirectoryExistsAsync(RelativePath path, CancellationToken cancellationToken)
        => Task.FromResult(Directory.Exists(ToFullPath(path)));

    public Task<FileEntry?> GetFileEntryAsync(RelativePath path, CancellationToken cancellationToken)
    {
        var fullPath = ToFullPath(path);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<FileEntry?>(null);
        }

        var info = new FileInfo(fullPath);
        return Task.FromResult<FileEntry?>(new FileEntry(path, false, info.Length, info.LastWriteTimeUtc));
    }

    public async Task DownloadAsync(RelativePath path, Stream destination, CancellationToken cancellationToken)
    {
        await using var source = File.OpenRead(ToFullPath(path));
        await source.CopyToAsync(destination, cancellationToken);
    }

    public async Task UploadAsync(RelativePath path, Stream source, CancellationToken cancellationToken)
    {
        var fullPath = ToFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? _root);
        await using var destination = File.Create(fullPath);
        await source.CopyToAsync(destination, cancellationToken);
    }

    public Task DeleteFileAsync(RelativePath path, CancellationToken cancellationToken)
    {
        File.Delete(ToFullPath(path));
        return Task.CompletedTask;
    }

    private string ToFullPath(RelativePath path)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, path.Value.Replace('/', Path.DirectorySeparatorChar)));
        var rootPath = Path.GetFullPath(_root);
        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("路径超出根目录。");
        }

        return fullPath;
    }
}
```

- [ ] **Step 4: Run tests and commit**

Run:

```powershell
dotnet run --project tests/FtpBackupUploadTool.Tests/FtpBackupUploadTool.Tests.csproj
git add src/FtpBackupUploadTool.Core/Models src/FtpBackupUploadTool.Core/Remote tests/FtpBackupUploadTool.Tests
git commit -m "feat: add file models and remote abstraction"
```

Expected test output includes:

```text
PASS LocalMirrorCanWriteAndRead
```

---

### Task 4: Add Config Store And DPAPI Password Protection

**Files:**
- Create: `src/FtpBackupUploadTool.Core/Security/IPasswordProtector.cs`
- Create: `src/FtpBackupUploadTool.Core/Security/DpapiPasswordProtector.cs`
- Create: `src/FtpBackupUploadTool.Core/Config/AppConfigStore.cs`
- Create: `tests/FtpBackupUploadTool.Tests/ConfigTests.cs`

- [ ] **Step 1: Write failing config tests**

Create `tests/FtpBackupUploadTool.Tests/ConfigTests.cs`:

```csharp
using FtpBackupUploadTool.Core.Config;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Security;

namespace FtpBackupUploadTool.Tests;

internal static class ConfigTests
{
    public static void PasswordRoundTripUsesProtector()
    {
        var protector = new InMemoryPasswordProtector();
        var encrypted = protector.Protect("secret");
        TestAssert.True(encrypted != "secret", "protected password should not be plain text");
        TestAssert.Equal("secret", protector.Unprotect(encrypted), "password should decrypt");
    }

    public static void ConfigRoundTripPreservesProcess()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ftp-tool-config", Guid.NewGuid().ToString("N"));
        var store = new AppConfigStore(Path.Combine(dir, "appsettings.json"));
        var config = new AppConfig(new[]
        {
            new ProcessConfig(
                "默认工序",
                new ServerConfig("prod", 21, "prod_user", "enc1", "/www/project"),
                new ServerConfig("draft", 21, "draft_user", "enc2", "/www/project"),
                @"D:\Release\project",
                @".\path-lists\default.txt",
                new BackupConfig("%USERPROFILE%\\Desktop", "{yyyy}{MM}{dd}_{HH}{mm}{ss}_Backup", LogFieldOptions.All))
        });

        store.SaveAsync(config, CancellationToken.None).GetAwaiter().GetResult();
        var loaded = store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.Equal("默认工序", loaded.Processes[0].Name, "process name should round trip");
        TestAssert.Equal("prod", loaded.Processes[0].ProductionServer.Host, "host should round trip");
    }

    private sealed class InMemoryPasswordProtector : IPasswordProtector
    {
        public string Protect(string plainText) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"protected:{plainText}"));
        public string Unprotect(string protectedText) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedText)).Replace("protected:", "", StringComparison.Ordinal);
    }
}
```

Add both `ConfigTests` methods to `Program.cs`.

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet run --project tests/FtpBackupUploadTool.Tests/FtpBackupUploadTool.Tests.csproj
```

Expected:

```text
The type or namespace name 'Config' does not exist
```

- [ ] **Step 3: Implement config and security**

Create `src/FtpBackupUploadTool.Core/Security/IPasswordProtector.cs`:

```csharp
namespace FtpBackupUploadTool.Core.Security;

public interface IPasswordProtector
{
    string Protect(string plainText);
    string Unprotect(string protectedText);
}
```

Create `src/FtpBackupUploadTool.Core/Security/DpapiPasswordProtector.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace FtpBackupUploadTool.Core.Security;

public sealed class DpapiPasswordProtector : IPasswordProtector
{
    public string Protect(string plainText)
    {
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public string Unprotect(string protectedText)
    {
        var bytes = Convert.FromBase64String(protectedText);
        var plainBytes = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
```

Create `src/FtpBackupUploadTool.Core/Config/AppConfigStore.cs`:

```csharp
using System.Text.Json;
using FtpBackupUploadTool.Core.Models;

namespace FtpBackupUploadTool.Core.Config;

public sealed record AppConfig(IReadOnlyList<ProcessConfig> Processes);

public sealed class AppConfigStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _configPath;

    public AppConfigStore(string configPath)
    {
        _configPath = configPath;
    }

    public static string GetDefaultConfigPath(string executableDirectory)
    {
        var portablePath = Path.Combine(executableDirectory, "config", "appsettings.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(portablePath) ?? executableDirectory);
            var probe = Path.Combine(Path.GetDirectoryName(portablePath) ?? executableDirectory, ".write-test");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return portablePath;
        }
        catch (UnauthorizedAccessException)
        {
            return GetAppDataPath();
        }
        catch (IOException)
        {
            return GetAppDataPath();
        }
    }

    public async Task<AppConfig> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_configPath))
        {
            return new AppConfig(Array.Empty<ProcessConfig>());
        }

        await using var stream = File.OpenRead(_configPath);
        var config = await JsonSerializer.DeserializeAsync<AppConfig>(stream, Options, cancellationToken);
        return config ?? new AppConfig(Array.Empty<ProcessConfig>());
    }

    public async Task SaveAsync(AppConfig config, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath) ?? ".");
        await using var stream = File.Create(_configPath);
        await JsonSerializer.SerializeAsync(stream, config, Options, cancellationToken);
    }

    private static string GetAppDataPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FtpBackupUploadTool", "appsettings.json");
    }
}
```

- [ ] **Step 4: Run tests and commit**

Run:

```powershell
dotnet run --project tests/FtpBackupUploadTool.Tests/FtpBackupUploadTool.Tests.csproj
git add src/FtpBackupUploadTool.Core/Security src/FtpBackupUploadTool.Core/Config tests/FtpBackupUploadTool.Tests
git commit -m "feat: add config store and password protection"
```

Expected test output includes:

```text
PASS PasswordRoundTripUsesProtector
PASS ConfigRoundTripPreservesProcess
```

---

### Task 5: Implement Backup Service And Backup Log Writer

**Files:**
- Create: `src/FtpBackupUploadTool.Core/Logging/BackupLogWriter.cs`
- Create: `src/FtpBackupUploadTool.Core/Services/BackupService.cs`
- Create: `tests/FtpBackupUploadTool.Tests/BackupServiceTests.cs`

- [ ] **Step 1: Write failing backup tests**

Create `tests/FtpBackupUploadTool.Tests/BackupServiceTests.cs`:

```csharp
using FtpBackupUploadTool.Core.Logging;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;
using FtpBackupUploadTool.Core.Remote;
using FtpBackupUploadTool.Core.Services;

namespace FtpBackupUploadTool.Tests;

internal static class BackupServiceTests
{
    public static void BackupDownloadsExistingAndLogsNewFile()
    {
        var productionRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-prod", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(productionRoot, "css"));
        File.WriteAllText(Path.Combine(productionRoot, "css", "site.css"), "prod-body");

        var backupRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-backup", Guid.NewGuid().ToString("N"));
        var service = new BackupService(new LocalMirrorRemoteClient(productionRoot), new BackupLogWriter());
        var paths = new[] { RelativePath.Parse("css/site.css"), RelativePath.Parse("js/new.js") };

        var result = service.RunAsync(paths, backupRoot, "{yyyy}{MM}{dd}_{HH}{mm}{ss}_Backup", LogFieldOptions.All, CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.True(File.Exists(Path.Combine(result.BackupFolder, "css", "site.css")), "existing production file should be backed up");
        TestAssert.True(File.Exists(Path.Combine(result.BackupFolder, "backup-log.csv")), "backup log should exist");
        var logText = File.ReadAllText(Path.Combine(result.BackupFolder, "backup-log.csv"));
        TestAssert.True(logText.Contains("新文件", StringComparison.Ordinal), "missing production file should be logged as new file");
    }
}
```

Add `BackupServiceTests.BackupDownloadsExistingAndLogsNewFile` to `Program.cs`.

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet run --project tests/FtpBackupUploadTool.Tests/FtpBackupUploadTool.Tests.csproj
```

Expected:

```text
The type or namespace name 'Services' does not exist
```

- [ ] **Step 3: Implement backup log writer**

Create `src/FtpBackupUploadTool.Core/Logging/BackupLogWriter.cs`:

```csharp
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
    public async Task WriteAsync(string logPath, IReadOnlyList<BackupLogRow> rows, LogFieldOptions fields, CancellationToken cancellationToken)
    {
        var lines = new List<string> { BuildHeader(fields) };
        lines.AddRange(rows.Select(row => BuildLine(row, fields)));
        await File.WriteAllLinesAsync(logPath, lines, new UTF8Encoding(true), cancellationToken);
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
                "FileSize" => row.FileSize?.ToString() ?? "",
                "LastModified" => row.LastModified?.ToString("u") ?? "",
                "Result" => row.Result,
                "ErrorMessage" => row.ErrorMessage,
                "Note" => row.Note,
                _ => ""
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
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }
}
```

- [ ] **Step 4: Implement backup service**

Create `src/FtpBackupUploadTool.Core/Services/BackupService.cs`:

```csharp
using FtpBackupUploadTool.Core.Logging;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;
using FtpBackupUploadTool.Core.Remote;

namespace FtpBackupUploadTool.Core.Services;

public sealed record BackupRunResult(string BackupFolder, IReadOnlyList<OperationLogEntry> Logs);

public sealed class BackupService
{
    private readonly IRemoteFileClient _production;
    private readonly BackupLogWriter _logWriter;

    public BackupService(IRemoteFileClient production, BackupLogWriter logWriter)
    {
        _production = production;
        _logWriter = logWriter;
    }

    public async Task<BackupRunResult> RunAsync(
        IReadOnlyList<RelativePath> paths,
        string backupRoot,
        string folderTemplate,
        LogFieldOptions logFields,
        CancellationToken cancellationToken)
    {
        var folder = Path.Combine(ExpandEnvironment(backupRoot), RenderFolderName(folderTemplate, DateTimeOffset.Now));
        Directory.CreateDirectory(folder);

        var logs = new List<OperationLogEntry>();
        var rows = new List<BackupLogRow>();
        foreach (var path in paths)
        {
            var entry = await _production.GetFileEntryAsync(path, cancellationToken);
            if (entry is null)
            {
                logs.Add(new OperationLogEntry(DateTimeOffset.Now, OperationLogLevel.Normal, "Backup", path, "新文件，跳过备份"));
                rows.Add(new BackupLogRow(path, path.Value, "", "", null, null, "Skipped", "", "新文件，生产服务器不存在，跳过备份"));
                continue;
            }

            var destinationPath = Path.Combine(folder, path.Value.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? folder);
            await using var destination = File.Create(destinationPath);
            await _production.DownloadAsync(path, destination, cancellationToken);
            logs.Add(new OperationLogEntry(DateTimeOffset.Now, OperationLogLevel.Normal, "Backup", path, "备份完成"));
            rows.Add(new BackupLogRow(path, path.Value, "", destinationPath, entry.Size, entry.LastModified, "BackedUp", "", ""));
        }

        await _logWriter.WriteAsync(Path.Combine(folder, "backup-log.csv"), rows, logFields, cancellationToken);
        return new BackupRunResult(folder, logs);
    }

    private static string RenderFolderName(string template, DateTimeOffset now)
    {
        return template
            .Replace("{yyyy}", now.ToString("yyyy"), StringComparison.Ordinal)
            .Replace("{MM}", now.ToString("MM"), StringComparison.Ordinal)
            .Replace("{dd}", now.ToString("dd"), StringComparison.Ordinal)
            .Replace("{HH}", now.ToString("HH"), StringComparison.Ordinal)
            .Replace("{mm}", now.ToString("mm"), StringComparison.Ordinal)
            .Replace("{ss}", now.ToString("ss"), StringComparison.Ordinal);
    }

    private static string ExpandEnvironment(string path)
    {
        return Environment.ExpandEnvironmentVariables(path);
    }
}
```

- [ ] **Step 5: Run tests and commit**

Run:

```powershell
dotnet run --project tests/FtpBackupUploadTool.Tests/FtpBackupUploadTool.Tests.csproj
git add src/FtpBackupUploadTool.Core/Logging src/FtpBackupUploadTool.Core/Services tests/FtpBackupUploadTool.Tests
git commit -m "feat: add production backup workflow"
```

Expected test output includes:

```text
PASS BackupDownloadsExistingAndLogsNewFile
```

---

### Task 6: Implement Upload Service

**Files:**
- Create: `src/FtpBackupUploadTool.Core/Services/UploadService.cs`
- Create: `tests/FtpBackupUploadTool.Tests/UploadServiceTests.cs`

- [ ] **Step 1: Write failing upload tests**

Create `tests/FtpBackupUploadTool.Tests/UploadServiceTests.cs`:

```csharp
using FtpBackupUploadTool.Core.Paths;
using FtpBackupUploadTool.Core.Remote;
using FtpBackupUploadTool.Core.Services;

namespace FtpBackupUploadTool.Tests;

internal static class UploadServiceTests
{
    public static void UploadCopiesLocalFileToDraft()
    {
        var localRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-local", Guid.NewGuid().ToString("N"));
        var draftRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-draft", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(localRoot, "css"));
        Directory.CreateDirectory(Path.Combine(draftRoot, "css"));
        File.WriteAllText(Path.Combine(localRoot, "css", "site.css"), "local-body");

        var service = new UploadService(new LocalMirrorRemoteClient(draftRoot), localRoot);
        var result = service.RunAsync(new[] { RelativePath.Parse("css/site.css") }, CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.Equal("local-body", File.ReadAllText(Path.Combine(draftRoot, "css", "site.css")), "draft file should match local");
        TestAssert.Equal(1, result.Logs.Count, "one log entry");
    }

    public static void UploadErrorsWhenDraftParentMissing()
    {
        var localRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-local", Guid.NewGuid().ToString("N"));
        var draftRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-draft", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(localRoot, "css"));
        File.WriteAllText(Path.Combine(localRoot, "css", "site.css"), "local-body");

        var service = new UploadService(new LocalMirrorRemoteClient(draftRoot), localRoot);
        var result = service.RunAsync(new[] { RelativePath.Parse("css/site.css") }, CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.Equal("Error", result.Logs[0].Level.ToString(), "missing parent should log error");
    }
}
```

Add both upload tests to `Program.cs`.

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet run --project tests/FtpBackupUploadTool.Tests/FtpBackupUploadTool.Tests.csproj
```

Expected:

```text
The type or namespace name 'UploadService' could not be found
```

- [ ] **Step 3: Implement upload service**

Create `src/FtpBackupUploadTool.Core/Services/UploadService.cs`:

```csharp
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;
using FtpBackupUploadTool.Core.Remote;

namespace FtpBackupUploadTool.Core.Services;

public sealed record UploadRunResult(IReadOnlyList<OperationLogEntry> Logs);

public sealed class UploadService
{
    private readonly IRemoteFileClient _draft;
    private readonly string _localRoot;

    public UploadService(IRemoteFileClient draft, string localRoot)
    {
        _draft = draft;
        _localRoot = localRoot;
    }

    public async Task<UploadRunResult> RunAsync(IReadOnlyList<RelativePath> paths, CancellationToken cancellationToken)
    {
        var logs = new List<OperationLogEntry>();
        foreach (var path in paths)
        {
            var localPath = ToLocalPath(path);
            if (!File.Exists(localPath))
            {
                logs.Add(new OperationLogEntry(DateTimeOffset.Now, OperationLogLevel.Error, "Upload", path, "本地文件不存在", localPath));
                continue;
            }

            var parent = GetParent(path);
            if (parent is not null && !await _draft.DirectoryExistsAsync(parent, cancellationToken))
            {
                logs.Add(new OperationLogEntry(DateTimeOffset.Now, OperationLogLevel.Error, "Upload", path, "起案服务器目标父文件夹不存在", parent.Value));
                continue;
            }

            await using var source = File.OpenRead(localPath);
            await _draft.UploadAsync(path, source, cancellationToken);
            logs.Add(new OperationLogEntry(DateTimeOffset.Now, OperationLogLevel.Normal, "Upload", path, "上传完成"));
        }

        return new UploadRunResult(logs);
    }

    private string ToLocalPath(RelativePath path)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_localRoot, path.Value.Replace('/', Path.DirectorySeparatorChar)));
        var root = Path.GetFullPath(_localRoot);
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("本地路径超出根目录。");
        }

        return fullPath;
    }

    private static RelativePath? GetParent(RelativePath path)
    {
        var index = path.Value.LastIndexOf('/');
        return index <= 0 ? null : RelativePath.Parse(path.Value[..index]);
    }
}
```

- [ ] **Step 4: Run tests and commit**

Run:

```powershell
dotnet run --project tests/FtpBackupUploadTool.Tests/FtpBackupUploadTool.Tests.csproj
git add src/FtpBackupUploadTool.Core/Services/UploadService.cs tests/FtpBackupUploadTool.Tests
git commit -m "feat: add draft upload workflow"
```

Expected test output includes:

```text
PASS UploadCopiesLocalFileToDraft
PASS UploadErrorsWhenDraftParentMissing
```

---

### Task 7: Implement Check Service

**Files:**
- Create: `src/FtpBackupUploadTool.Core/Services/CheckService.cs`
- Create: `tests/FtpBackupUploadTool.Tests/CheckServiceTests.cs`

- [ ] **Step 1: Write failing check tests**

Create `tests/FtpBackupUploadTool.Tests/CheckServiceTests.cs`:

```csharp
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;
using FtpBackupUploadTool.Core.Remote;
using FtpBackupUploadTool.Core.Services;

namespace FtpBackupUploadTool.Tests;

internal static class CheckServiceTests
{
    public static void CheckReturnsAllFourStatuses()
    {
        var prodRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-prod", Guid.NewGuid().ToString("N"));
        var draftRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-draft", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(prodRoot, "old"));
        Directory.CreateDirectory(Path.Combine(draftRoot, "css"));
        Directory.CreateDirectory(Path.Combine(draftRoot, "extra"));
        File.WriteAllText(Path.Combine(draftRoot, "css", "site.css"), "draft");
        File.WriteAllText(Path.Combine(draftRoot, "extra", "new.js"), "new");
        File.WriteAllText(Path.Combine(prodRoot, "old", "file.txt"), "prod");

        var service = new CheckService(new LocalMirrorRemoteClient(prodRoot), new LocalMirrorRemoteClient(draftRoot));
        var logs = service.RunAsync(new[]
        {
            RelativePath.Parse("css/site.css"),
            RelativePath.Parse("old/file.txt"),
            RelativePath.Parse("missing/file.txt")
        }, CancellationToken.None).GetAwaiter().GetResult().Logs;

        TestAssert.True(logs.Any(x => x.Level == OperationLogLevel.Normal && x.Path?.Value == "css/site.css"), "normal file update");
        TestAssert.True(logs.Any(x => x.Level == OperationLogLevel.Error && x.Path?.Value == "extra/new.js"), "path missing error");
        TestAssert.True(logs.Any(x => x.Level == OperationLogLevel.Warning && x.Path?.Value == "old/file.txt"), "new path old file warning");
        TestAssert.True(logs.Any(x => x.Level == OperationLogLevel.Error && x.Path?.Value == "missing/file.txt"), "missing file error");
    }
}
```

Add `CheckServiceTests.CheckReturnsAllFourStatuses` to `Program.cs`.

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet run --project tests/FtpBackupUploadTool.Tests/FtpBackupUploadTool.Tests.csproj
```

Expected:

```text
The type or namespace name 'CheckService' could not be found
```

- [ ] **Step 3: Implement check service**

Create `src/FtpBackupUploadTool.Core/Services/CheckService.cs`:

```csharp
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;
using FtpBackupUploadTool.Core.Remote;

namespace FtpBackupUploadTool.Core.Services;

public sealed record CheckRunResult(IReadOnlyList<OperationLogEntry> Logs);

public sealed class CheckService
{
    private readonly IRemoteFileClient _production;
    private readonly IRemoteFileClient _draft;

    public CheckService(IRemoteFileClient production, IRemoteFileClient draft)
    {
        _production = production;
        _draft = draft;
    }

    public async Task<CheckRunResult> RunAsync(IReadOnlyList<RelativePath> pathList, CancellationToken cancellationToken)
    {
        var logs = new List<OperationLogEntry>();
        var listed = pathList.ToDictionary(path => path.Value, path => path, StringComparer.OrdinalIgnoreCase);
        var draftFiles = await _draft.ListRecursiveAsync(cancellationToken);
        var draftSet = draftFiles.Where(file => !file.IsDirectory).ToDictionary(file => file.Path.Value, file => file.Path, StringComparer.OrdinalIgnoreCase);

        foreach (var draftPath in draftSet.Values)
        {
            if (!listed.ContainsKey(draftPath.Value))
            {
                logs.Add(new OperationLogEntry(DateTimeOffset.Now, OperationLogLevel.Error, "Check", draftPath, "路径缺失：起案服务器存在新文件"));
            }
        }

        foreach (var path in pathList)
        {
            if (draftSet.ContainsKey(path.Value))
            {
                logs.Add(new OperationLogEntry(DateTimeOffset.Now, OperationLogLevel.Normal, "Check", path, "文件更新：起案服务器存在路径对应文件"));
                continue;
            }

            if (await _production.FileExistsAsync(path, cancellationToken))
            {
                logs.Add(new OperationLogEntry(DateTimeOffset.Now, OperationLogLevel.Warning, "Check", path, "新路径旧文件：起案服务器缺失，生产服务器存在"));
            }
            else
            {
                logs.Add(new OperationLogEntry(DateTimeOffset.Now, OperationLogLevel.Error, "Check", path, "文件缺失：生产服务器和起案服务器均缺失"));
            }
        }

        return new CheckRunResult(logs);
    }
}
```

- [ ] **Step 4: Run tests and commit**

Run:

```powershell
dotnet run --project tests/FtpBackupUploadTool.Tests/FtpBackupUploadTool.Tests.csproj
git add src/FtpBackupUploadTool.Core/Services/CheckService.cs tests/FtpBackupUploadTool.Tests
git commit -m "feat: add check workflow"
```

Expected test output includes:

```text
PASS CheckReturnsAllFourStatuses
```

---

### Task 8: Implement FTP Remote Client

**Files:**
- Create: `src/FtpBackupUploadTool.Core/Remote/FtpRemoteFileClient.cs`
- Create: `src/FtpBackupUploadTool.Core/Remote/FtpPath.cs`

- [ ] **Step 1: Add FTP path helper**

Create `src/FtpBackupUploadTool.Core/Remote/FtpPath.cs`:

```csharp
using FtpBackupUploadTool.Core.Paths;

namespace FtpBackupUploadTool.Core.Remote;

public sealed class FtpPath
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _root;

    public FtpPath(string host, int port, string root)
    {
        _host = host;
        _port = port;
        _root = root.Trim('/');
    }

    public Uri For(RelativePath? path)
    {
        var relative = path?.Value.Trim('/') ?? "";
        var fullPath = string.IsNullOrEmpty(relative) ? _root : $"{_root}/{relative}";
        return new Uri($"ftp://{_host}:{_port}/{fullPath}");
    }
}
```

- [ ] **Step 2: Add FTP remote client**

Create `src/FtpBackupUploadTool.Core/Remote/FtpRemoteFileClient.cs`:

```csharp
using System.Net;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;

namespace FtpBackupUploadTool.Core.Remote;

public sealed class FtpRemoteFileClient : IRemoteFileClient
{
    private readonly FtpPath _paths;
    private readonly NetworkCredential _credential;

    public FtpRemoteFileClient(string host, int port, string root, string userName, string password)
    {
        _paths = new FtpPath(host, port, root);
        _credential = new NetworkCredential(userName, password);
    }

    public async Task<IReadOnlyList<FileEntry>> ListRecursiveAsync(CancellationToken cancellationToken)
    {
        var results = new List<FileEntry>();
        await ListDirectoryAsync(null, results, cancellationToken);
        return results;
    }

    public async Task<bool> FileExistsAsync(RelativePath path, CancellationToken cancellationToken)
    {
        return await GetFileEntryAsync(path, cancellationToken) is not null;
    }

    public async Task<bool> DirectoryExistsAsync(RelativePath path, CancellationToken cancellationToken)
    {
        try
        {
            _ = await ListNamesAsync(path, cancellationToken);
            return true;
        }
        catch (WebException)
        {
            return false;
        }
    }

    public async Task<FileEntry?> GetFileEntryAsync(RelativePath path, CancellationToken cancellationToken)
    {
        try
        {
            var request = CreateRequest(path, WebRequestMethods.Ftp.GetDateTimestamp);
            cancellationToken.ThrowIfCancellationRequested();
            using var response = (FtpWebResponse)await request.GetResponseAsync();
            return new FileEntry(path, false, 0, response.LastModified);
        }
        catch (WebException)
        {
            return null;
        }
    }

    public async Task DownloadAsync(RelativePath path, Stream destination, CancellationToken cancellationToken)
    {
        var request = CreateRequest(path, WebRequestMethods.Ftp.DownloadFile);
        cancellationToken.ThrowIfCancellationRequested();
        using var response = (FtpWebResponse)await request.GetResponseAsync();
        await using var source = response.GetResponseStream();
        if (source is null)
        {
            throw new IOException("FTP 下载响应没有数据流。");
        }

        await source.CopyToAsync(destination, cancellationToken);
    }

    public async Task UploadAsync(RelativePath path, Stream source, CancellationToken cancellationToken)
    {
        var request = CreateRequest(path, WebRequestMethods.Ftp.UploadFile);
        cancellationToken.ThrowIfCancellationRequested();
        await using var destination = await request.GetRequestStreamAsync();
        await source.CopyToAsync(destination, cancellationToken);
        using var response = (FtpWebResponse)await request.GetResponseAsync();
    }

    public async Task DeleteFileAsync(RelativePath path, CancellationToken cancellationToken)
    {
        var request = CreateRequest(path, WebRequestMethods.Ftp.DeleteFile);
        cancellationToken.ThrowIfCancellationRequested();
        using var response = (FtpWebResponse)await request.GetResponseAsync();
    }

    private async Task ListDirectoryAsync(RelativePath? directory, List<FileEntry> results, CancellationToken cancellationToken)
    {
        foreach (var name in await ListNamesAsync(directory, cancellationToken))
        {
            var child = RelativePath.Parse(directory is null ? name : $"{directory.Value}/{name}");
            if (await DirectoryExistsAsync(child, cancellationToken))
            {
                await ListDirectoryAsync(child, results, cancellationToken);
            }
            else
            {
                var entry = await GetFileEntryAsync(child, cancellationToken);
                if (entry is not null)
                {
                    results.Add(entry);
                }
            }
        }
    }

    private async Task<IReadOnlyList<string>> ListNamesAsync(RelativePath? directory, CancellationToken cancellationToken)
    {
        var request = CreateRequest(directory, WebRequestMethods.Ftp.ListDirectory);
        cancellationToken.ThrowIfCancellationRequested();
        using var response = (FtpWebResponse)await request.GetResponseAsync();
        using var stream = response.GetResponseStream();
        if (stream is null)
        {
            return Array.Empty<string>();
        }

        using var reader = new StreamReader(stream);
        var names = new List<string>();
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(line))
            {
                names.Add(line.Trim().Split('/').Last());
            }
        }

        return names;
    }

    private FtpWebRequest CreateRequest(RelativePath? path, string method)
    {
        var request = (FtpWebRequest)WebRequest.Create(_paths.For(path));
        request.Method = method;
        request.Credentials = _credential;
        request.UseBinary = true;
        request.KeepAlive = false;
        return request;
    }
}
```

- [ ] **Step 3: Build and commit**

Run:

```powershell
dotnet build FtpBackupUploadTool.sln
git add src/FtpBackupUploadTool.Core/Remote/FtpPath.cs src/FtpBackupUploadTool.Core/Remote/FtpRemoteFileClient.cs
git commit -m "feat: add FTP remote client"
```

Expected:

```text
Build succeeded.
```

---

### Task 9: Build Main WPF Layout And View Models

**Files:**
- Create: `src/FtpBackupUploadTool.App/Commands/RelayCommand.cs`
- Create: `src/FtpBackupUploadTool.App/ViewModels/FilePaneViewModel.cs`
- Create: `src/FtpBackupUploadTool.App/ViewModels/MainViewModel.cs`
- Modify: `src/FtpBackupUploadTool.App/MainWindow.xaml`
- Modify: `src/FtpBackupUploadTool.App/MainWindow.xaml.cs`

- [ ] **Step 1: Add command helper and view models**

Create `src/FtpBackupUploadTool.App/Commands/RelayCommand.cs`:

```csharp
using System.Windows.Input;

namespace FtpBackupUploadTool.App.Commands;

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool> _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute ?? (_ => true);
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute(parameter);

    public void Execute(object? parameter) => _execute(parameter);

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
```

Create `src/FtpBackupUploadTool.App/ViewModels/FilePaneViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using FtpBackupUploadTool.Core.Models;

namespace FtpBackupUploadTool.App.ViewModels;

public sealed class FilePaneViewModel
{
    public FilePaneViewModel(string title, bool isReadOnly)
    {
        Title = title;
        IsReadOnly = isReadOnly;
    }

    public string Title { get; }
    public bool IsReadOnly { get; }
    public string CurrentPath { get; set; } = "";
    public ObservableCollection<FileEntry> Entries { get; } = new();
}
```

Create `src/FtpBackupUploadTool.App/ViewModels/MainViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Windows.Input;
using FtpBackupUploadTool.App.Commands;
using FtpBackupUploadTool.Core.Models;

namespace FtpBackupUploadTool.App.ViewModels;

public sealed class MainViewModel
{
    public MainViewModel()
    {
        ProductionPane = new FilePaneViewModel("生产服务器", true);
        DraftPane = new FilePaneViewModel("起案服务器", false);
        LocalPane = new FilePaneViewModel("本地文件", false);
        BackupCommand = new RelayCommand(_ => Logs.Add("Backup clicked"));
        UploadCommand = new RelayCommand(_ => Logs.Add("Upload clicked"));
        CheckCommand = new RelayCommand(_ => Logs.Add("Check clicked"));
        OpenSettingsCommand = new RelayCommand(_ => SettingsRequested?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? SettingsRequested;

    public ObservableCollection<string> Processes { get; } = new() { "默认工序" };
    public string SelectedProcess { get; set; } = "默认工序";
    public string RootSummary { get; set; } = "根目录一致：/www/project | 本地：D:\\Release\\project";
    public string PathListText { get; set; } = "css/site.css\r\nimages/logo.png\r\ntemplates/index.html";
    public FilePaneViewModel ProductionPane { get; }
    public FilePaneViewModel DraftPane { get; }
    public FilePaneViewModel LocalPane { get; }
    public ObservableCollection<string> Logs { get; } = new();
    public ICommand BackupCommand { get; }
    public ICommand UploadCommand { get; }
    public ICommand CheckCommand { get; }
    public ICommand OpenSettingsCommand { get; }
}
```

- [ ] **Step 2: Replace main window XAML**

Replace `src/FtpBackupUploadTool.App/MainWindow.xaml` with:

```xml
<Window x:Class="FtpBackupUploadTool.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:FtpBackupUploadTool.App.ViewModels"
        Title="FTP备份上传工具" Height="760" Width="1280" MinWidth="1000" MinHeight="640">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="48" />
            <RowDefinition Height="*" />
            <RowDefinition Height="180" />
        </Grid.RowDefinitions>

        <DockPanel Grid.Row="0" Margin="10">
            <ComboBox Width="180" ItemsSource="{Binding Processes}" SelectedItem="{Binding SelectedProcess}" />
            <TextBox Margin="10,0" Text="{Binding RootSummary}" IsReadOnly="True" />
            <StackPanel DockPanel.Dock="Right" Orientation="Horizontal">
                <Button Content="备份" Width="76" Margin="4,0" Command="{Binding BackupCommand}" />
                <Button Content="上传" Width="76" Margin="4,0" Command="{Binding UploadCommand}" />
                <Button Content="Check" Width="76" Margin="4,0" Command="{Binding CheckCommand}" />
                <Button Content="设置" Width="76" Margin="4,0" Command="{Binding OpenSettingsCommand}" />
            </StackPanel>
        </DockPanel>

        <Grid Grid.Row="1" Margin="10,0,10,10">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="280" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>
            <GroupBox Header="路径清单" Margin="0,0,8,0">
                <TextBox Text="{Binding PathListText}" AcceptsReturn="True" VerticalScrollBarVisibility="Auto" FontFamily="Consolas" />
            </GroupBox>
            <GroupBox Grid.Column="1" Header="{Binding ProductionPane.Title}" Margin="0,0,8,0">
                <ListView ItemsSource="{Binding ProductionPane.Entries}" />
            </GroupBox>
            <GroupBox Grid.Column="2" Header="{Binding DraftPane.Title}" Margin="0,0,8,0">
                <ListView ItemsSource="{Binding DraftPane.Entries}" />
            </GroupBox>
            <GroupBox Grid.Column="3" Header="{Binding LocalPane.Title}">
                <ListView ItemsSource="{Binding LocalPane.Entries}" />
            </GroupBox>
        </Grid>

        <ListBox Grid.Row="2" Margin="10" ItemsSource="{Binding Logs}" Background="#101828" Foreground="#E5E7EB" FontFamily="Consolas" />
    </Grid>
</Window>
```

- [ ] **Step 3: Wire DataContext**

Replace `src/FtpBackupUploadTool.App/MainWindow.xaml.cs` with:

```csharp
using System.Windows;
using FtpBackupUploadTool.App.ViewModels;

namespace FtpBackupUploadTool.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.SettingsRequested += (_, _) =>
        {
            var window = new Views.SettingsWindow { Owner = this };
            window.ShowDialog();
        };
    }
}
```

- [ ] **Step 4: Build and commit**

Run:

```powershell
dotnet build FtpBackupUploadTool.sln
git add src/FtpBackupUploadTool.App
git commit -m "feat: add main WPF shell"
```

Expected:

```text
Build succeeded.
```

---

### Task 10: Build Settings Window

**Files:**
- Create: `src/FtpBackupUploadTool.App/ViewModels/SettingsViewModel.cs`
- Create: `src/FtpBackupUploadTool.App/Views/SettingsWindow.xaml`
- Create: `src/FtpBackupUploadTool.App/Views/SettingsWindow.xaml.cs`

- [ ] **Step 1: Add settings view model**

Create `src/FtpBackupUploadTool.App/ViewModels/SettingsViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Windows.Input;
using FtpBackupUploadTool.App.Commands;
using FtpBackupUploadTool.Core.Models;

namespace FtpBackupUploadTool.App.ViewModels;

public sealed class SettingsViewModel
{
    public SettingsViewModel()
    {
        Processes.Add("默认工序");
        SelectedProcess = "默认工序";
        LogFields = new ObservableCollection<LogFieldItem>
        {
            new("相对路径", true),
            new("生产完整路径", true),
            new("起案完整路径", true),
            new("本地完整路径", true),
            new("文件大小", true),
            new("最后修改时间", true),
            new("操作结果", true),
            new("错误信息", true),
            new("备注", true)
        };
        SaveCommand = new RelayCommand(_ => Saved?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? Saved;

    public ObservableCollection<string> Processes { get; } = new();
    public string SelectedProcess { get; set; }
    public string ProcessName { get; set; } = "默认工序";
    public string ProductionHost { get; set; } = "192.168.1.10";
    public string DraftHost { get; set; } = "192.168.1.20";
    public string ServerRoot { get; set; } = "/www/project";
    public string LocalRoot { get; set; } = @"D:\Release\project";
    public string BackupDirectory { get; set; } = "%USERPROFILE%\\Desktop";
    public string BackupTemplate { get; set; } = "{yyyy}{MM}{dd}_{HH}{mm}{ss}_Backup";
    public ObservableCollection<LogFieldItem> LogFields { get; }
    public ICommand SaveCommand { get; }
}

public sealed class LogFieldItem
{
    public LogFieldItem(string name, bool isChecked)
    {
        Name = name;
        IsChecked = isChecked;
    }

    public string Name { get; }
    public bool IsChecked { get; set; }
}
```

- [ ] **Step 2: Add settings window XAML**

Create `src/FtpBackupUploadTool.App/Views/SettingsWindow.xaml`:

```xml
<Window x:Class="FtpBackupUploadTool.App.Views.SettingsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="设置" Height="720" Width="1080" MinWidth="900" MinHeight="600">
    <DockPanel>
        <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" HorizontalAlignment="Right" Margin="10">
            <Button Content="取消" Width="80" Margin="4,0" IsCancel="True" />
            <Button Content="保存设置" Width="100" Margin="4,0" Command="{Binding SaveCommand}" />
        </StackPanel>
        <Grid Margin="10">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="240" />
                <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>
            <GroupBox Header="工序">
                <DockPanel>
                    <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Margin="6">
                        <Button Content="新增" Width="62" Margin="2" />
                        <Button Content="复制" Width="62" Margin="2" />
                        <Button Content="删除" Width="62" Margin="2" />
                    </StackPanel>
                    <ListBox ItemsSource="{Binding Processes}" SelectedItem="{Binding SelectedProcess}" />
                </DockPanel>
            </GroupBox>
            <ScrollViewer Grid.Column="1" Margin="10,0,0,0" VerticalScrollBarVisibility="Auto">
                <StackPanel>
                    <GroupBox Header="工序基础信息" Margin="0,0,0,10">
                        <TextBox Text="{Binding ProcessName}" Margin="10" />
                    </GroupBox>
                    <GroupBox Header="生产服务器 FTP" Margin="0,0,0,10">
                        <UniformGrid Columns="4" Margin="10">
                            <TextBox Text="{Binding ProductionHost}" Margin="4" />
                            <TextBox Text="21" Margin="4" />
                            <TextBox Text="prod_user" Margin="4" />
                            <PasswordBox Password="password" Margin="4" />
                        </UniformGrid>
                    </GroupBox>
                    <GroupBox Header="起案服务器 FTP" Margin="0,0,0,10">
                        <UniformGrid Columns="4" Margin="10">
                            <TextBox Text="{Binding DraftHost}" Margin="4" />
                            <TextBox Text="21" Margin="4" />
                            <TextBox Text="draft_user" Margin="4" />
                            <PasswordBox Password="password" Margin="4" />
                        </UniformGrid>
                    </GroupBox>
                    <GroupBox Header="根目录与备份" Margin="0,0,0,10">
                        <UniformGrid Columns="2" Margin="10">
                            <TextBox Text="{Binding ServerRoot}" Margin="4" />
                            <TextBox Text="{Binding LocalRoot}" Margin="4" />
                            <TextBox Text="{Binding BackupDirectory}" Margin="4" />
                            <TextBox Text="{Binding BackupTemplate}" Margin="4" />
                        </UniformGrid>
                    </GroupBox>
                    <GroupBox Header="备份日志内容">
                        <ItemsControl ItemsSource="{Binding LogFields}" Margin="10">
                            <ItemsControl.ItemTemplate>
                                <DataTemplate>
                                    <CheckBox Content="{Binding Name}" IsChecked="{Binding IsChecked}" Margin="4" />
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>
                    </GroupBox>
                </StackPanel>
            </ScrollViewer>
        </Grid>
    </DockPanel>
</Window>
```

- [ ] **Step 3: Add code-behind**

Create `src/FtpBackupUploadTool.App/Views/SettingsWindow.xaml.cs`:

```csharp
using System.Windows;
using FtpBackupUploadTool.App.ViewModels;

namespace FtpBackupUploadTool.App.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel = new();

    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.Saved += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }
}
```

- [ ] **Step 4: Build and commit**

Run:

```powershell
dotnet build FtpBackupUploadTool.sln
git add src/FtpBackupUploadTool.App/ViewModels/SettingsViewModel.cs src/FtpBackupUploadTool.App/Views
git commit -m "feat: add settings window"
```

Expected:

```text
Build succeeded.
```

---

### Task 11: Add File Pane Control Actions

**Files:**
- Create: `src/FtpBackupUploadTool.App/Controls/FilePaneControl.xaml`
- Create: `src/FtpBackupUploadTool.App/Controls/FilePaneControl.xaml.cs`
- Modify: `src/FtpBackupUploadTool.App/MainWindow.xaml`

- [ ] **Step 1: Create reusable file pane XAML**

Create `src/FtpBackupUploadTool.App/Controls/FilePaneControl.xaml`:

```xml
<UserControl x:Class="FtpBackupUploadTool.App.Controls.FilePaneControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             AllowDrop="True"
             Drop="OnDrop">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="32" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>
        <TextBox Grid.Row="0" Text="{Binding CurrentPath}" IsReadOnly="True" Margin="4" />
        <ListView Grid.Row="1" ItemsSource="{Binding Entries}" SelectionMode="Extended" Margin="4"
                  PreviewMouseMove="OnPreviewMouseMove"
                  ContextMenuOpening="OnContextMenuOpening">
            <ListView.ContextMenu>
                <ContextMenu>
                    <MenuItem Header="复制" Click="OnCopyClick" />
                    <MenuItem x:Name="PasteMenuItem" Header="粘贴" Click="OnPasteClick" />
                    <MenuItem x:Name="DeleteMenuItem" Header="删除" Click="OnDeleteClick" />
                </ContextMenu>
            </ListView.ContextMenu>
            <ListView.View>
                <GridView>
                    <GridViewColumn Header="名称" DisplayMemberBinding="{Binding Path.Value}" Width="220" />
                    <GridViewColumn Header="大小" DisplayMemberBinding="{Binding Size}" Width="80" />
                    <GridViewColumn Header="修改时间" DisplayMemberBinding="{Binding LastModified}" Width="150" />
                </GridView>
            </ListView.View>
        </ListView>
    </Grid>
</UserControl>
```

- [ ] **Step 2: Add code-behind for read-only enforcement**

Create `src/FtpBackupUploadTool.App/Controls/FilePaneControl.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FtpBackupUploadTool.App.ViewModels;
using FtpBackupUploadTool.Core.Models;

namespace FtpBackupUploadTool.App.Controls;

public partial class FilePaneControl : UserControl
{
    private Point _dragStart;

    public FilePaneControl()
    {
        InitializeComponent();
    }

    public event EventHandler<IReadOnlyList<FileEntry>>? CopyRequested;
    public event EventHandler<IReadOnlyList<FileEntry>>? PasteRequested;
    public event EventHandler<IReadOnlyList<FileEntry>>? DeleteRequested;
    public event EventHandler<IReadOnlyList<FileEntry>>? FilesDropped;

    private bool IsReadOnlyPane => DataContext is FilePaneViewModel viewModel && viewModel.IsReadOnly;

    private IReadOnlyList<FileEntry> SelectedEntries()
    {
        var listView = FindVisualChild<ListView>(this);
        return listView?.SelectedItems.OfType<FileEntry>().ToArray() ?? Array.Empty<FileEntry>();
    }

    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        PasteMenuItem.IsEnabled = !IsReadOnlyPane;
        DeleteMenuItem.IsEnabled = !IsReadOnlyPane;
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        CopyRequested?.Invoke(this, SelectedEntries());
    }

    private void OnPasteClick(object sender, RoutedEventArgs e)
    {
        if (IsReadOnlyPane)
        {
            MessageBox.Show("生产服务器只读，不能粘贴。", "只读窗口", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        PasteRequested?.Invoke(this, SelectedEntries());
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (IsReadOnlyPane)
        {
            MessageBox.Show("生产服务器只读，不能删除。", "只读窗口", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DeleteRequested?.Invoke(this, SelectedEntries());
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _dragStart = e.GetPosition(this);
            return;
        }

        var current = e.GetPosition(this);
        if (Math.Abs(current.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var selected = SelectedEntries();
        if (selected.Count > 0)
        {
            DragDrop.DoDragDrop(this, selected, DragDropEffects.Copy);
        }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (IsReadOnlyPane)
        {
            e.Effects = DragDropEffects.None;
            MessageBox.Show("生产服务器只读，不能拖拽写入。", "只读窗口", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (e.Data.GetData(typeof(FileEntry[])) is FileEntry[] entries)
        {
            FilesDropped?.Invoke(this, entries);
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T typed)
            {
                return typed;
            }

            var nested = FindVisualChild<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}
```

- [ ] **Step 3: Replace main window temporary file pane blocks**

In `src/FtpBackupUploadTool.App/MainWindow.xaml`, add the controls namespace:

```xml
xmlns:controls="clr-namespace:FtpBackupUploadTool.App.Controls"
```

Replace the three pane `ListView` blocks with:

```xml
<GroupBox Grid.Column="1" Header="{Binding ProductionPane.Title}" Margin="0,0,8,0">
    <controls:FilePaneControl DataContext="{Binding ProductionPane}" />
</GroupBox>
<GroupBox Grid.Column="2" Header="{Binding DraftPane.Title}" Margin="0,0,8,0">
    <controls:FilePaneControl DataContext="{Binding DraftPane}" />
</GroupBox>
<GroupBox Grid.Column="3" Header="{Binding LocalPane.Title}">
    <controls:FilePaneControl DataContext="{Binding LocalPane}" />
</GroupBox>
```

- [ ] **Step 4: Build and commit**

Run:

```powershell
dotnet build FtpBackupUploadTool.sln
git add src/FtpBackupUploadTool.App/Controls src/FtpBackupUploadTool.App/MainWindow.xaml
git commit -m "feat: add file pane interaction control"
```

Expected:

```text
Build succeeded.
```

---

### Task 12: Wire Core Services Into Main Commands

**Files:**
- Modify: `src/FtpBackupUploadTool.App/ViewModels/MainViewModel.cs`
- Modify: `src/FtpBackupUploadTool.App/App.xaml.cs`

- [ ] **Step 1: Replace main commands with async service calls**

Modify `MainViewModel.cs` so constructor accepts service dependencies:

```csharp
public MainViewModel(BackupService backupService, UploadService uploadService, CheckService checkService)
{
    ProductionPane = new FilePaneViewModel("生产服务器", true);
    DraftPane = new FilePaneViewModel("起案服务器", false);
    LocalPane = new FilePaneViewModel("本地文件", false);
    BackupCommand = new RelayCommand(async _ => await RunBackupAsync());
    UploadCommand = new RelayCommand(async _ => await RunUploadAsync());
    CheckCommand = new RelayCommand(async _ => await RunCheckAsync());
    OpenSettingsCommand = new RelayCommand(_ => SettingsRequested?.Invoke(this, EventArgs.Empty));
    _backupService = backupService;
    _uploadService = uploadService;
    _checkService = checkService;
}
```

Add private fields and command methods:

```csharp
private readonly BackupService _backupService;
private readonly UploadService _uploadService;
private readonly CheckService _checkService;

private IReadOnlyList<RelativePath> ParsePaths()
{
    return PathListParser.Parse(PathListText);
}

private async Task RunBackupAsync()
{
    var result = await _backupService.RunAsync(ParsePaths(), "%USERPROFILE%\\Desktop", "{yyyy}{MM}{dd}_{HH}{mm}{ss}_Backup", LogFieldOptions.All, CancellationToken.None);
    foreach (var log in result.Logs)
    {
        Logs.Add($"[{log.Level}] {log.Path}: {log.Message}");
    }
}

private async Task RunUploadAsync()
{
    var result = await _uploadService.RunAsync(ParsePaths(), CancellationToken.None);
    foreach (var log in result.Logs)
    {
        Logs.Add($"[{log.Level}] {log.Path}: {log.Message}");
    }
}

private async Task RunCheckAsync()
{
    var result = await _checkService.RunAsync(ParsePaths(), CancellationToken.None);
    foreach (var log in result.Logs)
    {
        Logs.Add($"[{log.Level}] {log.Path}: {log.Message}");
    }
}
```

- [ ] **Step 2: Wire local mirror services for first runnable build**

Modify `MainWindow.xaml.cs` to construct local mirror services for development:

```csharp
using System.Windows;
using FtpBackupUploadTool.App.ViewModels;
using FtpBackupUploadTool.Core.Logging;
using FtpBackupUploadTool.Core.Remote;
using FtpBackupUploadTool.Core.Services;

namespace FtpBackupUploadTool.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var devRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FtpBackupUploadTool", "DevMirror");
        var production = new LocalMirrorRemoteClient(Path.Combine(devRoot, "production"));
        var draft = new LocalMirrorRemoteClient(Path.Combine(devRoot, "draft"));
        var local = Path.Combine(devRoot, "local");
        Directory.CreateDirectory(local);

        var viewModel = new MainViewModel(
            new BackupService(production, new BackupLogWriter()),
            new UploadService(draft, local),
            new CheckService(production, draft));

        DataContext = viewModel;
        viewModel.SettingsRequested += (_, _) =>
        {
            var window = new Views.SettingsWindow { Owner = this };
            window.ShowDialog();
        };
    }
}
```

- [ ] **Step 3: Build and commit**

Run:

```powershell
dotnet build FtpBackupUploadTool.sln
git add src/FtpBackupUploadTool.App
git commit -m "feat: wire core workflows into UI commands"
```

Expected:

```text
Build succeeded.
```

---

### Task 13: Wire Saved Config To FTP Runtime

**Files:**
- Create: `src/FtpBackupUploadTool.App/Runtime/WorkflowServices.cs`
- Create: `src/FtpBackupUploadTool.App/Runtime/ProcessRuntimeFactory.cs`
- Modify: `src/FtpBackupUploadTool.App/ViewModels/MainViewModel.cs`
- Modify: `src/FtpBackupUploadTool.App/MainWindow.xaml.cs`

- [ ] **Step 1: Create runtime service container**

Create `src/FtpBackupUploadTool.App/Runtime/WorkflowServices.cs`:

```csharp
using FtpBackupUploadTool.Core.Services;

namespace FtpBackupUploadTool.App.Runtime;

public sealed record WorkflowServices(
    BackupService BackupService,
    UploadService UploadService,
    CheckService CheckService);
```

- [ ] **Step 2: Create process runtime factory**

Create `src/FtpBackupUploadTool.App/Runtime/ProcessRuntimeFactory.cs`:

```csharp
using FtpBackupUploadTool.Core.Logging;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Remote;
using FtpBackupUploadTool.Core.Security;
using FtpBackupUploadTool.Core.Services;

namespace FtpBackupUploadTool.App.Runtime;

public sealed class ProcessRuntimeFactory
{
    private readonly IPasswordProtector _passwordProtector;

    public ProcessRuntimeFactory(IPasswordProtector passwordProtector)
    {
        _passwordProtector = passwordProtector;
    }

    public WorkflowServices Create(ProcessConfig config)
    {
        var productionPassword = _passwordProtector.Unprotect(config.ProductionServer.EncryptedPassword);
        var draftPassword = _passwordProtector.Unprotect(config.DraftServer.EncryptedPassword);

        var production = new FtpRemoteFileClient(
            config.ProductionServer.Host,
            config.ProductionServer.Port,
            config.ProductionServer.RootPath,
            config.ProductionServer.UserName,
            productionPassword);

        var draft = new FtpRemoteFileClient(
            config.DraftServer.Host,
            config.DraftServer.Port,
            config.DraftServer.RootPath,
            config.DraftServer.UserName,
            draftPassword);

        return new WorkflowServices(
            new BackupService(production, new BackupLogWriter()),
            new UploadService(draft, config.LocalRootPath),
            new CheckService(production, draft));
    }
}
```

- [ ] **Step 3: Modify main view model to load process runtime**

In `src/FtpBackupUploadTool.App/ViewModels/MainViewModel.cs`, change service fields from `readonly` to mutable fields:

```csharp
private BackupService _backupService;
private UploadService _uploadService;
private CheckService _checkService;
private ProcessConfig? _currentProcess;
```

Add this method:

```csharp
public void LoadProcess(ProcessConfig config, WorkflowServices services)
{
    _currentProcess = config;
    _backupService = services.BackupService;
    _uploadService = services.UploadService;
    _checkService = services.CheckService;
    SelectedProcess = config.Name;
    RootSummary = $"根目录一致：{config.ProductionServer.RootPath} | 本地：{config.LocalRootPath}";
}
```

Change `RunBackupAsync` to use the selected process backup settings:

```csharp
private async Task RunBackupAsync()
{
    if (_currentProcess is null)
    {
        Logs.Add("[Error] 未选择工序，无法备份。");
        return;
    }

    var result = await _backupService.RunAsync(
        ParsePaths(),
        _currentProcess.Backup.BackupDirectory,
        _currentProcess.Backup.FolderNameTemplate,
        _currentProcess.Backup.LogFields,
        CancellationToken.None);

    foreach (var log in result.Logs)
    {
        Logs.Add($"[{log.Level}] {log.Path}: {log.Message}");
    }
}
```

- [ ] **Step 4: Load config on app startup**

Modify `src/FtpBackupUploadTool.App/MainWindow.xaml.cs`:

```csharp
using System.Windows;
using FtpBackupUploadTool.App.Runtime;
using FtpBackupUploadTool.App.ViewModels;
using FtpBackupUploadTool.Core.Config;
using FtpBackupUploadTool.Core.Logging;
using FtpBackupUploadTool.Core.Remote;
using FtpBackupUploadTool.Core.Security;
using FtpBackupUploadTool.Core.Services;

namespace FtpBackupUploadTool.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        var devRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FtpBackupUploadTool", "DevMirror");
        var production = new LocalMirrorRemoteClient(Path.Combine(devRoot, "production"));
        var draft = new LocalMirrorRemoteClient(Path.Combine(devRoot, "draft"));
        var local = Path.Combine(devRoot, "local");
        Directory.CreateDirectory(local);

        _viewModel = new MainViewModel(
            new BackupService(production, new BackupLogWriter()),
            new UploadService(draft, local),
            new CheckService(production, draft));

        DataContext = _viewModel;
        Loaded += OnLoaded;
        _viewModel.SettingsRequested += (_, _) =>
        {
            var window = new Views.SettingsWindow { Owner = this };
            window.ShowDialog();
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var configPath = AppConfigStore.GetDefaultConfigPath(AppContext.BaseDirectory);
        var store = new AppConfigStore(configPath);
        var config = await store.LoadAsync(CancellationToken.None);
        var selected = config.Processes.FirstOrDefault();
        if (selected is null)
        {
            _viewModel.Logs.Add("[Warning] 没有已保存工序，请先打开设置。");
            return;
        }

        var factory = new ProcessRuntimeFactory(new DpapiPasswordProtector());
        _viewModel.LoadProcess(selected, factory.Create(selected));
    }
}
```

- [ ] **Step 5: Build and commit**

Run:

```powershell
dotnet build FtpBackupUploadTool.sln
git add src/FtpBackupUploadTool.App/Runtime src/FtpBackupUploadTool.App/ViewModels/MainViewModel.cs src/FtpBackupUploadTool.App/MainWindow.xaml.cs
git commit -m "feat: wire saved FTP config into runtime"
```

Expected:

```text
Build succeeded.
```

---

### Task 14: Publish Portable Windows Build

**Files:**
- Create: `scripts/publish-portable.ps1`
- Create: `README.md`

- [ ] **Step 1: Add publish script**

Create `scripts/publish-portable.ps1`:

```powershell
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src/FtpBackupUploadTool.App/FtpBackupUploadTool.App.csproj'
$output = Join-Path $root 'artifacts/portable-win-x64'
New-Item -ItemType Directory -Force -Path $output | Out-Null
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $output
Write-Host "Portable build: $output"
```

- [ ] **Step 2: Add README**

Create `README.md`:

```markdown
# FTP 备份上传工具

Windows 便携版 FTP 备份、上传、Check 工具。

## 开发环境

- Windows
- .NET 8 SDK

## 构建

```powershell
dotnet build FtpBackupUploadTool.sln
```

## 测试

```powershell
dotnet run --project tests/FtpBackupUploadTool.Tests/FtpBackupUploadTool.Tests.csproj
```

## 发布便携 exe

```powershell
./scripts/publish-portable.ps1
```

输出目录：

```text
artifacts/portable-win-x64
```
```

- [ ] **Step 3: Publish and verify executable exists**

Run:

```powershell
./scripts/publish-portable.ps1
Test-Path .\artifacts\portable-win-x64\FtpBackupUploadTool.exe
```

Expected:

```text
True
```

- [ ] **Step 4: Commit publish assets**

Run:

```powershell
git add scripts/publish-portable.ps1 README.md
git commit -m "chore: add portable publish script"
```

---

### Task 15: Manual UI Verification

**Files:**
- Modify only files required to fix issues found by this task.

- [ ] **Step 1: Launch app**

Run:

```powershell
dotnet run --project src/FtpBackupUploadTool.App/FtpBackupUploadTool.App.csproj
```

Expected:

```text
The WPF main window opens with path list, three panes, and log list.
```

- [ ] **Step 2: Verify buttons**

In the WPF window:

- Click `备份`; expected: log list receives Backup entries or skipped-file entries.
- Click `上传`; expected: log list receives Upload entries or missing-parent errors.
- Click `Check`; expected: log list receives Normal, Warning, or Error entries.
- Click `设置`; expected: settings window opens.

- [ ] **Step 3: Verify production read-only UI behavior**

In the production pane, attempt write actions exposed by the UI. Expected:

- Delete action is disabled or unavailable.
- Paste action is disabled or unavailable.
- Drag/drop target rejects drops.

- [ ] **Step 4: Commit UI verification fixes**

If code changed:

```powershell
dotnet build FtpBackupUploadTool.sln
git add src
git commit -m "fix: polish first-run UI behavior"
```

If no code changed:

```powershell
git status --short
```

Expected:

```text
no output
```

---

## Self-Review Checklist

Spec coverage:

- Windows portable `.exe`: Task 14.
- FTP first version: Task 8.
- Core path rules and invalid `..`: Task 2.
- Three file panes and production read-only behavior: Tasks 9, 11, and 15.
- Backup workflow and backup log: Task 5.
- Upload workflow and missing target parent error: Task 6.
- Check four statuses and draft recursive scan: Task 7.
- Settings window: Task 10.
- Saved config to real FTP runtime: Task 13.
- Config storage and DPAPI password protection: Task 4.
- Testing: Tasks 2 through 7 plus Task 15.

Execution note:

- Task 12 wires local mirror clients first so the app is runnable before FTP credentials are saved.
- Task 13 replaces the command runtime with config-backed `FtpRemoteFileClient` construction after settings are saved.
