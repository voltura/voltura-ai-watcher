[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$assetDirectory = Join-Path $projectRoot "installer\assets"
[System.IO.Directory]::CreateDirectory($assetDirectory) | Out-Null

$background = [System.Drawing.Color]::FromArgb(4, 10, 7)
$panel = [System.Drawing.Color]::FromArgb(8, 24, 14)
$panelDeep = [System.Drawing.Color]::FromArgb(5, 17, 10)
$grid = [System.Drawing.Color]::FromArgb(18, 78, 39)
$border = [System.Drawing.Color]::FromArgb(49, 166, 83)
$accent = [System.Drawing.Color]::FromArgb(92, 255, 126)
$muted = [System.Drawing.Color]::FromArgb(67, 139, 84)

function New-Canvas {
    param([int]$Width, [int]$Height)

    $bitmap = [System.Drawing.Bitmap]::new(
        $Width,
        $Height,
        [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $graphics.Clear($background)
    return @($bitmap, $graphics)
}

function Draw-Grid {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Width,
        [int]$Height,
        [int]$Step
    )

    $pen = [System.Drawing.Pen]::new($grid, 1)
    try {
        for ($x = 0; $x -lt $Width; $x += $Step) {
            $Graphics.DrawLine($pen, $x, 0, $x, $Height)
        }
        for ($y = 0; $y -lt $Height; $y += $Step) {
            $Graphics.DrawLine($pen, 0, $y, $Width, $y)
        }
    }
    finally {
        $pen.Dispose()
    }
}

function Save-Bitmap24 {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [string]$Path
    )

    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Bmp)
}

$headerParts = New-Canvas -Width 150 -Height 57
$header = $headerParts[0]
$g = $headerParts[1]
try {
    Draw-Grid -Graphics $g -Width 150 -Height 57 -Step 10
    $panelBrush = [System.Drawing.SolidBrush]::new($panelDeep)
    $borderPen = [System.Drawing.Pen]::new($border, 1)
    $accentBrush = [System.Drawing.SolidBrush]::new($accent)
    $mutedBrush = [System.Drawing.SolidBrush]::new($muted)
    $titleFont = [System.Drawing.Font]::new("Bahnschrift SemiCondensed", 9, [System.Drawing.FontStyle]::Bold)
    $smallFont = [System.Drawing.Font]::new("Bahnschrift SemiCondensed", 5.5, [System.Drawing.FontStyle]::Regular)
    try {
        $g.FillRectangle($panelBrush, 5, 6, 140, 45)
        $g.DrawRectangle($borderPen, 5, 6, 139, 44)
        $g.FillEllipse($accentBrush, 12, 15, 8, 8)
        $g.DrawString("VOLTURA AI WATCHER", $titleFont, $accentBrush, 27, 11)
        $g.DrawString("LIVE CODEX ACTIVITY STREAM", $smallFont, $mutedBrush, 28, 29)
        $g.DrawLine($borderPen, 28, 41, 135, 41)
        $g.DrawLine($borderPen, 28, 45, 104, 45)
    }
    finally {
        $panelBrush.Dispose()
        $borderPen.Dispose()
        $accentBrush.Dispose()
        $mutedBrush.Dispose()
        $titleFont.Dispose()
        $smallFont.Dispose()
    }
}
finally {
    $g.Dispose()
}
Save-Bitmap24 -Bitmap $header -Path (Join-Path $assetDirectory "installer-header.bmp")
$header.Dispose()

$welcomeParts = New-Canvas -Width 164 -Height 314
$welcome = $welcomeParts[0]
$g = $welcomeParts[1]
try {
    Draw-Grid -Graphics $g -Width 164 -Height 314 -Step 10
    $panelBrush = [System.Drawing.SolidBrush]::new($panel)
    $deepBrush = [System.Drawing.SolidBrush]::new($panelDeep)
    $accentBrush = [System.Drawing.SolidBrush]::new($accent)
    $mutedBrush = [System.Drawing.SolidBrush]::new($muted)
    $borderPen = [System.Drawing.Pen]::new($border, 1)
    $mutedPen = [System.Drawing.Pen]::new($muted, 1)
    $titleFont = [System.Drawing.Font]::new("Bahnschrift SemiCondensed", 7.5, [System.Drawing.FontStyle]::Bold)
    $tinyFont = [System.Drawing.Font]::new("Bahnschrift SemiCondensed", 5, [System.Drawing.FontStyle]::Regular)
    $aiFont = [System.Drawing.Font]::new("Bahnschrift SemiCondensed", 18, [System.Drawing.FontStyle]::Bold)
    try {
        $g.FillRectangle($deepBrush, 7, 9, 150, 222)
        $g.DrawRectangle($borderPen, 7, 9, 149, 221)
        $g.FillEllipse($accentBrush, 14, 18, 6, 6)
        $g.DrawString("VOLTURA // AI WATCHER", $titleFont, $accentBrush, 26, 14)
        $g.DrawString("CODEX MESSAGE STREAM", $tinyFont, $mutedBrush, 15, 31)

        $rows = @(
            @{ Y = 45; Status = "WORKING"; Width = 104 },
            @{ Y = 83; Status = "WAITING FOR INPUT"; Width = 86 },
            @{ Y = 121; Status = "COMPLETED"; Width = 112 },
            @{ Y = 159; Status = "APPROVAL"; Width = 72 }
        )
        foreach ($row in $rows) {
            $g.FillRectangle($panelBrush, 13, $row.Y, 138, 32)
            $g.DrawRectangle($mutedPen, 13, $row.Y, 137, 31)
            $g.FillEllipse($accentBrush, 19, $row.Y + 7, 5, 5)
            $g.DrawString($row.Status, $tinyFont, $accentBrush, 29, $row.Y + 4)
            $g.DrawLine($mutedPen, 29, $row.Y + 16, 29 + $row.Width, $row.Y + 16)
            $g.DrawLine($mutedPen, 29, $row.Y + 22, 115, $row.Y + 22)
        }

        $g.DrawString("FILTER // CHAT", $tinyFont, $mutedBrush, 15, 203)
        $g.DrawLine($borderPen, 61, 207, 146, 207)
        $g.DrawLine($borderPen, 15, 218, 146, 218)

        $g.FillEllipse($deepBrush, 48, 242, 68, 58)
        $g.DrawEllipse($borderPen, 48, 242, 68, 58)
        $g.DrawEllipse($mutedPen, 55, 249, 54, 44)
        $g.DrawString("AI", $aiFont, $accentBrush, 68, 253)
        $g.DrawLine($borderPen, 82, 233, 82, 242)
        $g.DrawLine($borderPen, 82, 300, 82, 309)
        $g.DrawLine($borderPen, 38, 271, 48, 271)
        $g.DrawLine($borderPen, 116, 271, 126, 271)
        $g.FillEllipse($accentBrush, 79, 229, 6, 6)
        $g.FillEllipse($accentBrush, 79, 306, 6, 6)
        $g.FillEllipse($accentBrush, 34, 268, 6, 6)
        $g.FillEllipse($accentBrush, 124, 268, 6, 6)
    }
    finally {
        $panelBrush.Dispose()
        $deepBrush.Dispose()
        $accentBrush.Dispose()
        $mutedBrush.Dispose()
        $borderPen.Dispose()
        $mutedPen.Dispose()
        $titleFont.Dispose()
        $tinyFont.Dispose()
        $aiFont.Dispose()
    }
}
finally {
    $g.Dispose()
}
Save-Bitmap24 -Bitmap $welcome -Path (Join-Path $assetDirectory "installer-welcome.bmp")
$welcome.Dispose()

Write-Host "Generated Voltura AI Watcher installer artwork."
