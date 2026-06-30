# GitHub release versioning

## Normal pushes

Every push to `main` runs the GitHub Actions workflow in `.github/workflows/release.yml`.
The workflow builds a portable Windows ZIP and stores it as a GitHub Actions artifact.

For normal pushes, the executable version is generated from the GitHub run number:

```text
1.0.<run_number>.0
```

## Official releases

Official releases are managed with Git tags.

To publish version `1.0.1`:

```powershell
git tag v1.0.1
git push origin v1.0.1
```

Pushing the tag triggers the workflow, builds the portable ZIP, writes the executable
file version as `1.0.1.0`, creates a GitHub Release named `v1.0.1`, and uploads the ZIP.

Use this version pattern:

```text
v1.0.1  bug fix
v1.1.0  small feature release
v2.0.0  major release
```

## Local publish with a version

The same version properties can be passed locally:

```powershell
./scripts/publish-portable.ps1 `
  -Version "1.0.1" `
  -FileVersion "1.0.1.0" `
  -InformationalVersion "1.0.1+local"
```

The portable executable is written to:

```text
artifacts/portable-win-x64/FTP BU Tool.exe
```
