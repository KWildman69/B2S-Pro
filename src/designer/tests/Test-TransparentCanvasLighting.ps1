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
$bounds = New-Object System.Drawing.Rectangle(0, 0, 3, 3)

function New-SolidBitmap([System.Drawing.Color]$color) {
    $bitmap = New-Object System.Drawing.Bitmap(3, 3, $pixelFormat)
    for ($y = 0; $y -lt 3; $y++) {
        for ($x = 0; $x -lt 3; $x++) {
            $bitmap.SetPixel($x, $y, $color)
        }
    }
    return $bitmap
}

function Render-TestLight([System.Drawing.Bitmap]$source, [bool]$transmitTransparentCanvas) {
    $template = New-SolidBitmap ([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
    try {
        return [B2SBackglassDesigner.Illumination.ArtworkFlasherRenderer]::RenderFromAlphaField(
            $source,
            $template,
            800,
            [System.Drawing.Color]::White,
            0,
            100,
            0,
            100,
            140,
            0,
            0,
            0,
            0,
            0,
            0,
            $transmitTransparentCanvas)
    }
    finally {
        $template.Dispose()
    }
}

function Assert-Alpha([string]$name, [System.Drawing.Image]$image, [scriptblock]$predicate) {
    if ($null -eq $image) {
        throw "$name returned no image."
    }
    $bitmap = [System.Drawing.Bitmap]$image
    $alpha = $bitmap.GetPixel(1, 1).A
    if (-not (& $predicate $alpha)) {
        throw "$name failed with center alpha $alpha."
    }
    Write-Host "${name}: center alpha $alpha"
}

$transparentBlack = New-SolidBitmap ([System.Drawing.Color]::FromArgb(0, 0, 0, 0))
$opaqueBlack = New-SolidBitmap ([System.Drawing.Color]::FromArgb(255, 0, 0, 0))
$opaqueWhite = New-SolidBitmap ([System.Drawing.Color]::FromArgb(255, 255, 255, 255))

$legacyTransparent = $null
$behindTransparent = $null
$behindOpaqueBlack = $null
$behindOpaqueWhite = $null
$clippedOpen = $null
$clippedOpaque = $null

try {
    $legacyTransparent = Render-TestLight $transparentBlack $false
    $behindTransparent = Render-TestLight $transparentBlack $true
    $behindOpaqueBlack = Render-TestLight $opaqueBlack $true
    $behindOpaqueWhite = Render-TestLight $opaqueWhite $true

    Assert-Alpha 'Legacy front-light transparency remains unchanged' $legacyTransparent { param($a) $a -eq 0 }
    Assert-Alpha 'Transparent canvas opening transmits behind-canvas light' $behindTransparent { param($a) $a -gt 0 }
    Assert-Alpha 'Opaque black artwork remains protected' $behindOpaqueBlack { param($a) $a -eq 0 }
    Assert-Alpha 'Opaque light artwork still renders before canvas clipping' $behindOpaqueWhite { param($a) $a -gt 0 }

    $clippedOpen = [B2SBackglassDesigner.Illumination.Lights]::CreateCanvasClippedSnippet(
        $behindTransparent, $transparentBlack, $bounds)
    $clippedOpaque = [B2SBackglassDesigner.Illumination.Lights]::CreateCanvasClippedSnippet(
        $behindOpaqueWhite, $opaqueWhite, $bounds)

    Assert-Alpha 'Canvas clipping preserves light in transparent opening' $clippedOpen { param($a) $a -gt 0 }
    Assert-Alpha 'Canvas clipping removes light beneath opaque artwork' $clippedOpaque { param($a) $a -eq 0 }

    Write-Host 'PASS: transparent-canvas lighting regression checks passed.'
}
finally {
    @($legacyTransparent, $behindTransparent, $behindOpaqueBlack, $behindOpaqueWhite,
      $clippedOpen, $clippedOpaque, $transparentBlack, $opaqueBlack, $opaqueWhite) |
        Where-Object { $null -ne $_ } |
        ForEach-Object { $_.Dispose() }
}
