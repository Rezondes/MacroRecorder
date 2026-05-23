<#
.SYNOPSIS
  Sets the app version used for releases (csproj + project map).

.DESCRIPTION
  Updates MacroRecorder.App.csproj (Version, AssemblyVersion, FileVersion) and
  syncs .cursor/map/project-map.md status / next release step.

  CI reads Version from the csproj; the Git tag must be v<Version> (e.g. v0.0.3).

.PARAMETER Version
  Semantic version X.Y.Z (optional leading "v" is stripped).

.EXAMPLE
  .\scripts\set-app-version.ps1 0.0.3
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $Version
)

$ErrorActionPreference = "Stop"
$Version = $Version.Trim().TrimStart("v", "V")
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must be X.Y.Z (e.g. 0.0.3). Got: '$Version'"
}

$assemblyVersion = "$Version.0"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$appProj = Join-Path $repoRoot "MacroRecorder.App\MacroRecorder.App.csproj"
$projectMap = Join-Path $repoRoot ".cursor\map\project-map.md"

if (-not (Test-Path -LiteralPath $appProj)) {
    throw "App project not found: $appProj"
}

$oldVersion = [string](
    dotnet msbuild $appProj -getProperty:Version -nologo -v:q
).Trim()

function Set-XmlProperty([string] $content, [string] $elementName, [string] $value) {
    $pattern = "<$elementName>[^<]*</$elementName>"
    $replacement = "<$elementName>$value</$elementName>"
    if ($content -notmatch $pattern) {
        throw "Could not find <$elementName> in $appProj"
    }
    return [regex]::Replace($content, $pattern, $replacement, 1)
}

$projContent = Get-Content -LiteralPath $appProj -Raw -Encoding UTF8
$projContent = Set-XmlProperty $projContent "Version" $Version
$projContent = Set-XmlProperty $projContent "AssemblyVersion" $assemblyVersion
$projContent = Set-XmlProperty $projContent "FileVersion" $assemblyVersion
Set-Content -LiteralPath $appProj -Value $projContent -Encoding UTF8 -NoNewline

$verifiedVersion = [string](
    dotnet msbuild $appProj -getProperty:Version -nologo -v:q
).Trim()
if ($verifiedVersion -ne $Version) {
    throw "Version verification failed. Expected '$Version', got '$verifiedVersion'."
}

if (Test-Path -LiteralPath $projectMap) {
    $mapContent = Get-Content -LiteralPath $projectMap -Raw -Encoding UTF8
    $mapContent = [regex]::Replace($mapContent, 'Version `\d+\.\d+\.\d+`', "Version ``$Version``")
    $mapContent = [regex]::Replace($mapContent, 'Tag `v\d+\.\d+\.\d+`', "Tag ``v$Version``")

    if ($oldVersion -and $oldVersion -ne $Version) {
        $oldPending = "- [ ] Release ``v$oldVersion``"
        $oldDone = "- [x] Release ``v$oldVersion``"
        if ($mapContent -match [regex]::Escape($oldPending)) {
            $mapContent = $mapContent.Replace($oldPending, $oldDone)
        }
    }

    $newPending = "- [ ] Release ``v$Version`` pushen"
    if ($mapContent -match '- \[ \] Release `v[\d\.]+`') {
        $mapContent = [regex]::Replace(
            $mapContent,
            '- \[ \] Release `v[\d\.]+`[^\r\n]*',
            $newPending)
    }
    elseif ($mapContent -notmatch [regex]::Escape($newPending)) {
        $mapContent = $mapContent.TrimEnd() + "`r`n$newPending`r`n"
    }

    Set-Content -LiteralPath $projectMap -Value $mapContent -Encoding UTF8 -NoNewline
}

Write-Host "Version set to $Version (was $oldVersion)" -ForegroundColor Green
Write-Host "  $appProj" -ForegroundColor DarkGray
if (Test-Path -LiteralPath $projectMap) {
    Write-Host "  $projectMap" -ForegroundColor DarkGray
}
Write-Host ""
Write-Host "Next: commit, push, then:" -ForegroundColor Cyan
Write-Host "  git tag v$Version" -ForegroundColor Cyan
Write-Host "  git push origin v$Version" -ForegroundColor Cyan
