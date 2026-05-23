<#
.SYNOPSIS
  Renders MacroRecorder.App/Assets/app-icon.ico

.EXAMPLE
  .\scripts\build-app-icon.ps1
#>
$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$outDir = Join-Path $repoRoot "MacroRecorder.App\Assets"
$outIco = Join-Path $outDir "app-icon.ico"

Add-Type -AssemblyName System.Drawing

function New-BrandBitmap([int] $size) {
    $bitmap = New-Object Drawing.Bitmap $size, $size
    $bitmap.SetResolution(96, 96)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [Drawing.Text.TextRenderingHint]::ClearTypeGridFit
    $graphics.Clear([Drawing.Color]::Transparent)

    $inset = [int]($size * 16 / 256)
    $radius = [int]($size * 48 / 256)
    $rect = New-Object Drawing.Rectangle ($inset, $inset, ($size - 2 * $inset), ($size - 2 * $inset))

    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $diameter = $radius * 2
    $path.AddArc($rect.X, $rect.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($rect.Right - $diameter, $rect.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($rect.Right - $diameter, $rect.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()

    $brush = New-Object Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [Drawing.ColorTranslator]::FromHtml("#78909C"),
        [Drawing.ColorTranslator]::FromHtml("#455A64"),
        135)
    $graphics.FillPath($brush, $path)

    $fontSize = $size * 96 / 256
    $font = New-Object Drawing.Font("Segoe UI", [single]$fontSize, [Drawing.FontStyle]::Bold)
    $text = "MR"
    $textSize = $graphics.MeasureString($text, $font)
    $x = ($size - $textSize.Width) / 2
    $y = ($size - $textSize.Height) / 2
    $graphics.DrawString($text, $font, [Drawing.Brushes]::White, $x, $y)

    $graphics.Dispose()
    $brush.Dispose()
    $path.Dispose()
    $font.Dispose()
    return $bitmap
}

New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$bitmap = New-BrandBitmap -size 256
$icon = [Drawing.Icon]::FromHandle($bitmap.GetHicon())
$fileStream = [IO.File]::Create($outIco)
try { $icon.Save($fileStream) } finally { $fileStream.Dispose() }
$bitmap.Dispose()
Write-Host "Wrote $outIco" -ForegroundColor Green
