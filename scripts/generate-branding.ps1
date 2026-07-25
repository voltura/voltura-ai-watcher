[CmdletBinding()]
param(
    [switch]$SkipScreenshot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$masterPath = Join-Path $repoRoot "assets\branding\voltura-ai-watcher-master.png"
$docsAssetDirectory = Join-Path $repoRoot "docs\assets"
$readmeMarkPath = Join-Path $docsAssetDirectory "voltura-ai-watcher.png"
$socialPreviewPath = Join-Path $docsAssetDirectory "voltura-ai-watcher-social-preview.png"

if (-not (Test-Path -LiteralPath $masterPath -PathType Leaf))
{
    throw "Branding master was not found: $masterPath"
}

& (Join-Path $PSScriptRoot "generate-icon.ps1")
& (Join-Path $PSScriptRoot "generate-installer-images.ps1")

Add-Type -AssemblyName System.Drawing
[System.IO.Directory]::CreateDirectory($docsAssetDirectory) | Out-Null
$master = [System.Drawing.Bitmap]::new($masterPath)
try
{
    $mark = [System.Drawing.Bitmap]::new(512, 512, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try
    {
        $graphics = [System.Drawing.Graphics]::FromImage($mark)
        try
        {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.DrawImage($master, 0, 0, 512, 512)
        }
        finally
        {
            $graphics.Dispose()
        }
        $mark.Save($readmeMarkPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally
    {
        $mark.Dispose()
    }

    $preview = [System.Drawing.Bitmap]::new(1280, 640, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try
    {
        $graphics = [System.Drawing.Graphics]::FromImage($preview)
        try
        {
            $graphics.Clear([System.Drawing.Color]::FromArgb(3, 10, 6))
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $gridPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(24, 85, 45), 1)
            $accentBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(124, 255, 154))
            $mutedBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(89, 153, 105))
            $titleFont = [System.Drawing.Font]::new("Bahnschrift SemiCondensed", 38, [System.Drawing.FontStyle]::Bold)
            $subtitleFont = [System.Drawing.Font]::new("Bahnschrift SemiCondensed", 20, [System.Drawing.FontStyle]::Regular)
            try
            {
                for ($x = 0; $x -lt 1280; $x += 32) { $graphics.DrawLine($gridPen, $x, 0, $x, 640) }
                for ($y = 0; $y -lt 640; $y += 32) { $graphics.DrawLine($gridPen, 0, $y, 1280, $y) }
                $graphics.DrawImage($master, 72, 64, 512, 512)
                $graphics.DrawString("VOLTURA // AI WATCHER", $titleFont, $accentBrush, 620, 204)
                $graphics.DrawString("LOCAL CODEX ACTIVITY, AT A GLANCE", $subtitleFont, $mutedBrush, 626, 294)
                $graphics.DrawLine([System.Drawing.Pens]::LimeGreen, 626, 345, 1160, 345)
            }
            finally
            {
                $gridPen.Dispose()
                $accentBrush.Dispose()
                $mutedBrush.Dispose()
                $titleFont.Dispose()
                $subtitleFont.Dispose()
            }
        }
        finally
        {
            $graphics.Dispose()
        }
        $preview.Save($socialPreviewPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally
    {
        $preview.Dispose()
    }
}
finally
{
    $master.Dispose()
}

Write-Host "Generated Voltura AI Watcher branding."
