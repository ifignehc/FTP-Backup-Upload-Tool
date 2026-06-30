param(
    [string]$Version = '',
    [string]$FileVersion = '',
    [string]$InformationalVersion = ''
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src/FtpBackupUploadTool.App/FtpBackupUploadTool.App.csproj'
$output = Join-Path $root 'artifacts/portable-win-x64'

New-Item -ItemType Directory -Force -Path $output | Out-Null

$publishArgs = @(
    $project,
    '-c',
    'Release',
    '-r',
    'win-x64',
    '--self-contained',
    'true',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-o',
    $output
)

if ($Version -ne '') {
    $publishArgs += "-p:Version=$Version"
}

if ($FileVersion -ne '') {
    $publishArgs += "-p:AssemblyVersion=$FileVersion"
    $publishArgs += "-p:FileVersion=$FileVersion"
}

if ($InformationalVersion -ne '') {
    $publishArgs += "-p:InformationalVersion=$InformationalVersion"
}

dotnet publish @publishArgs
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
