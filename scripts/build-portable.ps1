<#
.SYNOPSIS
  Builds a self-contained win-x64 single-file portable ZIP for Macro Recorder.

.DESCRIPTION
  Publishes MacroRecorder.App to artifacts\portable\staging, then creates
  artifacts\portable\MacroRecorder-portable-win-x64-<Version>.zip.

  Extract the ZIP anywhere and run "MacroRecorderByRezondes.exe".
  The folder also contains "MacroRecorderByRezondes.Updater.exe" for in-app updates.
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
$updaterProj = Join-Path $repoRoot "MacroRecorder.Updater\MacroRecorder.Updater.csproj"
$stagingDir = Join-Path $repoRoot "artifacts\portable\staging"
$outputDir = Join-Path $repoRoot "artifacts\portable"
$expectedMainExeName = "MacroRecorderByRezondes.exe"
$expectedUpdaterExeName = "MacroRecorderByRezondes.Updater.exe"

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
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=none `
        -p:DebugSymbols=false `
        -o $stagingDir

    dotnet publish $updaterProj `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:DebugType=none `
        -p:DebugSymbols=false `
        -o $stagingDir

    $stagingFiles = Get-ChildItem -LiteralPath $stagingDir -File
    $stagingFileCount = @($stagingFiles).Count
    if ($stagingFileCount -ne 2) {
        throw "Expected exactly 2 portable executables in staging, but found $stagingFileCount file(s)."
    }

    $publishedMainExe = Join-Path $stagingDir $expectedMainExeName
    if (-not (Test-Path -LiteralPath $publishedMainExe)) {
        throw "Expected published executable not found: $publishedMainExe"
    }

    $publishedUpdaterExe = Join-Path $stagingDir $expectedUpdaterExeName
    if (-not (Test-Path -LiteralPath $publishedUpdaterExe)) {
        throw "Expected published updater executable not found: $publishedUpdaterExe"
    }

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
Write-Host "Published: $expectedMainExeName, $expectedUpdaterExeName" -ForegroundColor Green
