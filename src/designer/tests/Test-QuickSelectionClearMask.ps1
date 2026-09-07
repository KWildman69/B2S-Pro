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

$pixelFormat = [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
$background = New-Object System.Drawing.Bitmap(32, 32, $pixelFormat)
$mask = New-Object System.Drawing.Bitmap(32, 32, $pixelFormat)
$graphics = [System.Drawing.Graphics]::FromImage($mask)
$stream = New-Object System.IO.MemoryStream
$form = $null

try {
    $graphics.Clear([System.Drawing.Color]::White)
    $graphics.Dispose()
    $graphics = $null
    $mask.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)

    $bulb = New-Object B2SBackglassDesigner.Illumination.BulbInfo
    $bulb.SelectionMaskData = [Convert]::ToBase64String($stream.ToArray())
    $form = New-Object B2SBackglassDesigner.formQuickSelection($bulb, $background)

    $flags = [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::NonPublic
    $clearMethod = $form.GetType().GetMethod('ClearSelection', $flags)
    $closeMethod = $form.GetType().GetMethod('OnFormClosing', $flags)
    if ($null -eq $clearMethod -or $null -eq $closeMethod) {
        throw 'Quick Selection clear/commit methods could not be inspected.'
    }

    [void]$clearMethod.Invoke($form, [object[]]@($null, [System.EventArgs]::Empty))
    $form.DialogResult = [System.Windows.Forms.DialogResult]::OK
    $closing = New-Object System.Windows.Forms.FormClosingEventArgs(
        [System.Windows.Forms.CloseReason]::None, $false)
    [void]$closeMethod.Invoke($form, [object[]]@($closing.PSObject.BaseObject))

    if (-not [string]::IsNullOrEmpty($bulb.SelectionMaskData)) {
        throw "Clear Mask committed an active all-transparent mask ($($bulb.SelectionMaskData.Length) Base64 characters)."
    }
    if (-not $bulb.IsIlluminatedImageDirty) {
        throw 'Clear Mask did not invalidate the light image.'
    }

    Write-Host 'PASS: Clear Mask commits no mask and restores normal light rendering.'
}
finally {
    if ($null -ne $form) { $form.Dispose() }
    if ($null -ne $graphics) { $graphics.Dispose() }
    $stream.Dispose()
    $mask.Dispose()
    $background.Dispose()
}
