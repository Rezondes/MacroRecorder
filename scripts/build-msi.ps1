<#
.SYNOPSIS
  Builds self-contained win-x64 MSIs (WiX) for Macro Recorder — German and English.

.DESCRIPTION
  Restores and builds MacroRecorder.Installer twice with InstallerWixCulture=de-DE and en-US.
  WiX links one .wxl per MSI; passing both wxl files in one build causes duplicate string ids (WIX0100).

  The installer project uses a per-culture intermediate path so MSBuild IncrementalClean does not remove
  the other culture's MSI when building the second language.

  Outputs:
    MacroRecorder.Installer\bin\<Configuration>\de-DE\MacroRecorderSetup.msi
    MacroRecorder.Installer\bin\<Configuration>\en-US\MacroRecorderSetup.msi

  To install the MSI that matches Windows UI language (de -> German MSI, else English):
    .\scripts\install-msi.ps1

  The MSI uses a vendored WiX UI (based on WixUI_InstallDir): after choosing the
  install folder, an extra dialog offers two checkboxes (defaults on): start the
  app after setup, and create a shortcut on the common (public) desktop. The
  shortcut is created via a post-InstallFiles script (ICE-safe); uninstall removes it.

  Re-running the MSI on an installed product offers repair/remove. Upgrades: bump
  Version in MacroRecorder.Installer/Package.wxs (keep the same UpgradeCode).

  Silent install with a custom folder (admin / elevated prompt as needed):
    msiexec /i "...\MacroRecorderSetup.msi" INSTALLFOLDER="D:\Apps\MacroRecorder" /qn

.PARAMETER Configuration
  MSBuild configuration (default: Release).

.EXAMPLE
  .\scripts\build-msi.ps1
#>
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$installerProj = Join-Path $repoRoot "MacroRecorder.Installer\MacroRecorder.Installer.wixproj"

Push-Location $repoRoot
try {
    dotnet restore $installerProj
    dotnet build $installerProj -c $Configuration --no-restore /p:InstallerWixCulture=de-DE
    dotnet build $installerProj -c $Configuration --no-restore /p:InstallerWixCulture=en-US
}
finally {
    Pop-Location
}

$msiDe = Join-Path $repoRoot "MacroRecorder.Installer\bin\$Configuration\de-DE\MacroRecorderSetup.msi"
$msiEn = Join-Path $repoRoot "MacroRecorder.Installer\bin\$Configuration\en-US\MacroRecorderSetup.msi"
Write-Host "MSI (de-DE): $msiDe" -ForegroundColor Green
Write-Host "MSI (en-US): $msiEn" -ForegroundColor Green
