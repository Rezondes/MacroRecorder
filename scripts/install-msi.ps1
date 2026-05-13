<#
.SYNOPSIS
  Starts setup using the German or English MSI that matches Windows UI language.

.DESCRIPTION
  Chooses the MSI under MacroRecorder.Installer\bin\<Configuration>\<culture>\MacroRecorderSetup.msi:
  CurrentUICulture two-letter language "de" -> de-DE MSI; "en" or any other language -> en-US MSI
  (same rule as the app: only German is localized separately; otherwise English).

  Build both MSIs first: .\scripts\build-msi.ps1

  Remaining arguments are passed to msiexec (e.g. /qn, INSTALLFOLDER="D:\Apps\MacroRecorder").

.PARAMETER Configuration
  MSBuild configuration folder under bin (default: Release).

.EXAMPLE
  .\scripts\install-msi.ps1

.EXAMPLE
  .\scripts\install-msi.ps1 /qn
#>
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$twoLetter = [System.Globalization.CultureInfo]::CurrentUICulture.TwoLetterISOLanguageName
$cultureFolder = if ($twoLetter -eq "de") { "de-DE" } else { "en-US" }
$msi = Join-Path $repoRoot "MacroRecorder.Installer\bin\$Configuration\$cultureFolder\MacroRecorderSetup.msi"

if (-not (Test-Path -LiteralPath $msi)) {
    Write-Error "MSI not found: $msi. Run .\scripts\build-msi.ps1 -Configuration $Configuration first."
}

Write-Host "Using MSI ($cultureFolder): $msi" -ForegroundColor Green
& msiexec.exe @("/i", $msi) @args
