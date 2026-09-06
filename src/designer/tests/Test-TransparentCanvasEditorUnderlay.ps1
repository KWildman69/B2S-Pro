param(
    [Parameter(Mandatory = $true)]
    [string]$AssemblyPath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $AssemblyPath)) {
    throw "B2S Pro assembly not found: $AssemblyPath"
}

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
[void][System.Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $AssemblyPath))

$source = New-Object System.Drawing.Bitmap(2, 1, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb))
$source.SetPixel(0, 0, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
$source.SetPixel(1, 0, [System.Drawing.Color]::FromArgb(255, 0, 0, 0))

$canvas = $null
$preview = $null
try {
    [B2SBackglassDesigner.Backglass]::currentData = New-Object B2SBackglassDesigner.Backglass+Data
    $canvas = New-Object B2SBackglassDesigner.B2SPictureBox
    $canvas.Image = $source
    $preview = $canvas.CreateLayeredPreviewImage()

    if ($null -eq $preview) {
        throw 'The editor canvas preview returned no image.'
    }

    $transparentPixel = $preview.GetPixel(0, 0)
    $opaqueBlackPixel = $preview.GetPixel(1, 0)

    $neutralUnderlay = [System.Drawing.Color]::FromArgb(255, 96, 96, 96)
    if ($transparentPixel.ToArgb() -ne $neutralUnderlay.ToArgb()) {
        throw "Transparent canvas pixel was not displayed over neutral gray: $transparentPixel"
    }
    if ($opaqueBlackPixel.ToArgb() -ne [System.Drawing.Color]::Black.ToArgb()) {
        throw "Opaque black artwork changed unexpectedly: $opaqueBlackPixel"
    }

    Write-Host "Transparent canvas pixel: $transparentPixel"
    Write-Host "Opaque black artwork: $opaqueBlackPixel"
    Write-Host 'PASS: editor canvas uses a neutral-gray transparency underlay without changing opaque black artwork.'
}
finally {
    if ($null -ne $preview) { $preview.Dispose() }
    if ($null -ne $canvas) { $canvas.Dispose() }
    $source.Dispose()
    [B2SBackglassDesigner.Backglass]::currentData = $null
}
