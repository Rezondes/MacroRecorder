<#
.SYNOPSIS
  Builds a self-contained win-x64 portable ZIP for Macro Recorder.

.DESCRIPTION
  Publishes MacroRecorder.App to artifacts\portable\staging, then creates
  artifacts\portable\MacroRecorder-portable-win-x64-<Version>.zip.

  Extract the ZIP anywhere and run "Macro Recorder by Rezondes.exe".
  User data (macros, settings) stays in %LocalAppData%\MacroRecorderByRezondes\.

.PARAMETER Configuration
  MSBuild configuration (default: Release).

.EXAMPLE
  .\scripts\build-portable.ps1
#>
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$appProj = Join-Path $repoRoot "MacroRecorder.App\MacroRecorder.App.csproj"
$stagingDir = Join-Path $repoRoot "artifacts\portable\staging"
$outputDir = Join-Path $repoRoot "artifacts\portable"

$version = [string](
    dotnet msbuild $appProj -getProperty:Version -nologo -v:q
).Trim()
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Could not read Version from $appProj"
}

$zipName = "MacroRecorder-portable-win-x64-$version.zip"
$zipPath = Join-Path $outputDir $zipName

Push-Location $repoRoot
try {
    if (Test-Path -LiteralPath $stagingDir) {
        Remove-Item -LiteralPath $stagingDir -Recurse -Force
    }

    dotnet publish $appProj `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -o $stagingDir

    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    Compress-Archive -Path (Join-Path $stagingDir "*") -DestinationPath $zipPath -CompressionLevel Optimal
}
finally {
    Pop-Location
}

Write-Host "Portable ZIP: $zipPath" -ForegroundColor Green
Write-Host "Version: $version" -ForegroundColor Green
