param(
    [Parameter(Mandatory = $true)]
    [string]$AssemblyPath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $AssemblyPath)) {
    throw "B2S Server assembly not found: $AssemblyPath"
}

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
[void][System.Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $AssemblyPath))

function New-ReelFrame([System.Drawing.Color]$Color) {
    $bitmap = New-Object System.Drawing.Bitmap(64, 48, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb))
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear($Color)
    }
    finally {
        $graphics.Dispose()
    }
    return $bitmap
}

function Render-Reel([B2S.B2SReelBox]$Reel) {
    $bitmap = New-Object System.Drawing.Bitmap(64, 48, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb))
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::Black)
    }
    finally {
        $graphics.Dispose()
    }
    $Reel.DrawToBitmap($bitmap, (New-Object System.Drawing.Rectangle(0, 0, 64, 48)))
    return $bitmap
}

function Get-Luminance([System.Drawing.Color]$Color) {
    return 0.2126 * $Color.R + 0.7152 * $Color.G + 0.0722 * $Color.B
}

$source = New-ReelFrame ([System.Drawing.Color]::FromArgb(255, 170, 170, 170))
$legacyIlluminatedSource = New-ReelFrame ([System.Drawing.Color]::FromArgb(255, 40, 40, 180))
$reel = New-Object B2S.B2SReelBox
$inactive = $null
$active = $null
$alwaysOn = $null
$legacyActive = $null

try {
    [B2S.B2SData]::ReelImages.Clear()
    [B2S.B2SData]::ReelIlluImages.Clear()
    [B2S.B2SData]::ReelIntermediateImages.Clear()
    [B2S.B2SData]::ReelIntermediateIlluImages.Clear()
    [B2S.B2SData]::ReelImages.Add('TestReel_0', $source)

    $reel.Size = New-Object System.Drawing.Size(64, 48)
    $reel.BackColor = [System.Drawing.Color]::Black
    $reel.ReelType = 'TestReel'
    $reel.Reel3DEnabled = $true
    $reel.Reel3DBrightness = 140
    $reel.Reel3DTemperature = 4000
    $reel.Reel3DDepth = 100
    $reel.Reel3DGlass = 0
    $reel.SetID = 0
    $reel.RomID = 30
    $reel.RomIDValue = 2

    $reel.Illuminated = $false
    $inactive = Render-Reel $reel
    $inactiveLuminance = Get-Luminance ($inactive.GetPixel(32, 24))

    # Illumination Location Off produces no legacy illuminated-image set. The
    # active Player Up state must therefore fall back to the normal reel image.
    $reel.Illuminated = $true
    $active = Render-Reel $reel
    $activeLuminance = Get-Luminance ($active.GetPixel(32, 24))

    if ($inactiveLuminance -le 1) {
        throw 'The inactive triggered 3D reel lost its visible digit image.'
    }
    if ($activeLuminance -le $inactiveLuminance + 20) {
        throw "The Player Up state did not switch on the 3D backlight (inactive=$inactiveLuminance, active=$activeLuminance)."
    }

    # A 3D reel without a trigger retains the original always-backlit behavior.
    $reel.RomID = 0
    $reel.Illuminated = $false
    $alwaysOn = Render-Reel $reel
    $alwaysOnLuminance = Get-Luminance ($alwaysOn.GetPixel(32, 24))
    if ([Math]::Abs($alwaysOnLuminance - $activeLuminance) -gt 2) {
        throw "An untriggered 3D reel no longer stays backlit (active=$activeLuminance, untriggered=$alwaysOnLuminance)."
    }

    # Existing backglasses with a legacy illuminated-image set must continue to
    # use that alternate source while their trigger is active.
    [B2S.B2SData]::ReelIlluImages.Add('TestReel_0_1', $legacyIlluminatedSource)
    $reel.RomID = 30
    $reel.SetID = 1
    $reel.Illuminated = $true
    $legacyActive = Render-Reel $reel
    $legacyPixel = $legacyActive.GetPixel(32, 24)
    if ($legacyPixel.B -le $legacyPixel.R + 30) {
        throw "The active reel stopped using its legacy illuminated-image set: $legacyPixel"
    }

    Write-Host "Inactive triggered reel luminance: $([Math]::Round($inactiveLuminance, 1))"
    Write-Host "Active Player Up reel luminance: $([Math]::Round($activeLuminance, 1))"
    Write-Host "Untriggered always-on luminance: $([Math]::Round($alwaysOnLuminance, 1))"
    Write-Host "Legacy illuminated-set pixel: $legacyPixel"
    Write-Host 'PASS: Player Up controls only the 3D backlight, and Illumination Location Off keeps the digits visible.'
}
finally {
    if ($inactive -ne $null) { $inactive.Dispose() }
    if ($active -ne $null) { $active.Dispose() }
    if ($alwaysOn -ne $null) { $alwaysOn.Dispose() }
    if ($legacyActive -ne $null) { $legacyActive.Dispose() }
    $reel.Dispose()
    [B2S.B2SData]::ReelImages.Clear()
    [B2S.B2SData]::ReelIlluImages.Clear()
    [B2S.B2SData]::ReelIntermediateImages.Clear()
    [B2S.B2SData]::ReelIntermediateIlluImages.Clear()
    $source.Dispose()
    $legacyIlluminatedSource.Dispose()
}
