$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src/FtpBackupUploadTool.App/FtpBackupUploadTool.App.csproj'
$output = Join-Path $root 'artifacts/portable-win-x64'

New-Item -ItemType Directory -Force -Path $output | Out-Null

dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $output
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$defaultExe = Join-Path $output 'FtpBackupUploadTool.exe'
$portableExe = Join-Path $output 'FTP BU Tool.exe'

if (Test-Path -LiteralPath $portableExe) {
    Remove-Item -LiteralPath $portableExe -Force
}

Move-Item -LiteralPath $defaultExe -Destination $portableExe

Write-Host "Portable build: $portableExe"
