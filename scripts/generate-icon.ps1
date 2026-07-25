[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$trayMasterPath = Join-Path $repoRoot "assets\branding\voltura-ai-watcher-master.png"
$outputPath = Join-Path $repoRoot "VolturaAiWatcher\Assets\voltura-ai-watcher.ico"
$sizes = @(16, 24, 32, 48, 256)

if (-not (Test-Path -LiteralPath $trayMasterPath -PathType Leaf))
{
    throw "Tray branding master was not found: $trayMasterPath"
}

$trayMaster = [System.Drawing.Bitmap]::new($trayMasterPath)
try
{
    if ($trayMaster.Width -lt 256 -or $trayMaster.Height -lt 256)
    {
        throw "Tray branding master must be at least 256x256; received $($trayMaster.Width)x$($trayMaster.Height)."
    }

    $images = [System.Collections.Generic.List[byte[]]]::new()
    foreach ($size in $sizes)
    {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try
        {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try
            {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

                $graphics.DrawImage($trayMaster, 0, 0, $size, $size)
            }
            finally
            {
                $graphics.Dispose()
            }

            $stream = [System.IO.MemoryStream]::new()
            try
            {
                $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
                $images.Add($stream.ToArray())
            }
            finally
            {
                $stream.Dispose()
            }
        }
        finally
        {
            $bitmap.Dispose()
        }
    }

    [System.IO.Directory]::CreateDirectory((Split-Path $outputPath -Parent)) | Out-Null
    $file = [System.IO.File]::Create($outputPath)
    try
    {
        $writer = [System.IO.BinaryWriter]::new($file)
        try
        {
            $writer.Write([uint16]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]$images.Count)
            $offset = 6 + (16 * $images.Count)
            for ($index = 0; $index -lt $images.Count; $index++)
            {
                $size = $sizes[$index]
                $writer.Write([byte]($(if ($size -eq 256) { 0 } else { $size })))
                $writer.Write([byte]($(if ($size -eq 256) { 0 } else { $size })))
                $writer.Write([byte]0)
                $writer.Write([byte]0)
                $writer.Write([uint16]1)
                $writer.Write([uint16]32)
                $writer.Write([uint32]$images[$index].Length)
                $writer.Write([uint32]$offset)
                $offset += $images[$index].Length
            }

            foreach ($image in $images)
            {
                $writer.Write($image)
            }
        }
        finally
        {
            $writer.Dispose()
        }
    }
    finally
    {
        $file.Dispose()
    }
}
finally
{
    $trayMaster.Dispose()
}

Write-Host "Created Voltura AI Watcher icon: $outputPath"
