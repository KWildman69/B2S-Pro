param(
    [Parameter(Mandatory = $true)]
    [string]$AssemblyPath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $AssemblyPath)) {
    throw "B2S Pro assembly not found: $AssemblyPath"
}

Add-Type -AssemblyName System.Drawing
[void][System.Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $AssemblyPath))

$pixelFormat = [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
$bounds = New-Object System.Drawing.Rectangle(0, 0, 160, 80)
$creator = New-Object B2SBackglassDesigner.Illumination.Create

function New-SolidBitmap([System.Drawing.Color]$color) {
    $bitmap = New-Object System.Drawing.Bitmap(160, 80, $pixelFormat)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear($color)
    }
    finally {
        $graphics.Dispose()
    }
    return $bitmap
}

function Invoke-NormalLamp(
    [System.Drawing.Bitmap]$background,
    [string]$text,
    [System.Drawing.Font]$font,
    [string]$maskData) {
    return $creator.CreateOverlayImage(
        $background, $bounds, $bounds, 1,
        [System.Drawing.Color]::White, [System.Drawing.Color]::White,
        $text, $font,
        [B2SBackglassDesigner.Illumination.eTextAlignment]::Center,
        [B2SBackglassDesigner.Illumination.eIlluMode]::Standard,
        60, 60, 100, $maskData, 0, $false,
        0, 100, 0, 0, 50, 50, 0, 4000,
        $false, 140, 0, $false, 0, 0, 0, 0, 0, 0.0, $false)
}

$blackBackground = New-SolidBitmap ([System.Drawing.Color]::Black)
$transparentBackground = New-SolidBitmap ([System.Drawing.Color]::Transparent)
$font = New-Object System.Drawing.Font('Arial', 28, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$mask = New-SolidBitmap ([System.Drawing.Color]::Transparent)
$maskGraphics = [System.Drawing.Graphics]::FromImage($mask)
$maskStream = New-Object System.IO.MemoryStream
$textLamp = $null
$maskedLamp = $null

try {
    $maskGraphics.FillRectangle([System.Drawing.Brushes]::White, 0, 0, 80, 80)
    $maskGraphics.Dispose()
    $maskGraphics = $null
    $mask.Save($maskStream, [System.Drawing.Imaging.ImageFormat]::Png)
    $maskData = [Convert]::ToBase64String($maskStream.ToArray())

    $textLamp = Invoke-NormalLamp $blackBackground 'TEST' $font ''
    $maximumAlpha = 0
    $solidTextPixels = 0
    for ($y = 0; $y -lt $textLamp.Height; $y++) {
        for ($x = 0; $x -lt $textLamp.Width; $x++) {
            $alpha = $textLamp.GetPixel($x, $y).A
            $maximumAlpha = [Math]::Max($maximumAlpha, $alpha)
            if ($alpha -ge 200) { $solidTextPixels++ }
        }
    }
    if ($maximumAlpha -lt 200 -or $solidTextPixels -lt 50) {
        throw "Normal text lamp lost its sharp text layer (max alpha $maximumAlpha, solid pixels $solidTextPixels)."
    }
    Write-Host "Normal text lamp: max alpha $maximumAlpha, solid text pixels $solidTextPixels"

    $maskedLamp = Invoke-NormalLamp $transparentBackground '' $null $maskData
    $insideAlpha = $maskedLamp.GetPixel(60, 40).A
    $outsideAlpha = $maskedLamp.GetPixel(120, 40).A
    if ($insideAlpha -le 0) {
        throw 'Quick Selection removed the entire normal lamp instead of clipping it.'
    }
    if ($outsideAlpha -ne 0) {
        throw "Quick Selection leaked outside its mask (outside alpha $outsideAlpha)."
    }
    Write-Host "Quick Selection lamp: inside alpha $insideAlpha, outside alpha $outsideAlpha"
    Write-Host 'PASS: normal text and Quick Selection lamps use the lamp renderer.'
}
finally {
    if ($null -ne $textLamp) { $textLamp.Dispose() }
    if ($null -ne $maskedLamp) { $maskedLamp.Dispose() }
    if ($null -ne $maskGraphics) { $maskGraphics.Dispose() }
    $maskStream.Dispose()
    $mask.Dispose()
    $font.Dispose()
    $blackBackground.Dispose()
    $transparentBackground.Dispose()
}
