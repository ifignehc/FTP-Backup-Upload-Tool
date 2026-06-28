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

## 发布便携版 exe

```powershell
./scripts/publish-portable.ps1
```

如果本机 PowerShell 执行策略拦截脚本，可以使用：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-portable.ps1
```

输出目录：

```text
artifacts/portable-win-x64
```
