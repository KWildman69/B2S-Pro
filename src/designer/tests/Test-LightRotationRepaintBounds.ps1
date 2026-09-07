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
$assembly = [System.Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $AssemblyPath))

$canvas = New-Object B2SBackglassDesigner.B2SPictureBox
$canvas.Image = New-Object System.Drawing.Bitmap(400, 300)
$bulb = New-Object B2SBackglassDesigner.Illumination.BulbInfo
$bulb.Location = New-Object System.Drawing.Point(100, 100)
$bulb.Size = New-Object System.Drawing.Size(120, 40)
$bulb.GlowSpread = 20

try {
    $method = $canvas.GetType().GetMethod(
        'LightRotationEditorBounds',
        [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::NonPublic)
    $normalBoundsMethod = $canvas.GetType().GetMethod(
        'BulbEditorBounds',
        [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::NonPublic)
    if ($null -eq $method) {
        throw 'The pending-angle light rotation repaint helper is missing.'
    }
    if ($null -eq $normalBoundsMethod) {
        throw 'The normal bulb editor repaint helper is missing.'
    }

    $rawBulb = $bulb.PSObject.BaseObject
    $atZero = [System.Drawing.Rectangle]$method.Invoke($canvas, [object[]]@($rawBulb, [single]1.0, [single]0.0))
    $atNinety = [System.Drawing.Rectangle]$method.Invoke($canvas, [object[]]@($rawBulb, [single]1.0, [single]90.0))
    $topHandle = New-Object System.Drawing.Point(160, 76)
    $rightHandle = New-Object System.Drawing.Point(204, 120)

    if (-not $atZero.Contains($topHandle)) {
        throw "Zero-degree repaint bounds omit the top rotation handle: $atZero"
    }
    if (-not $atNinety.Contains($rightHandle)) {
        throw "Ninety-degree repaint bounds omit the right rotation handle: $atNinety"
    }
    if ($atZero.Equals($atNinety)) {
        throw 'Rotation repaint bounds still ignore the pending drag angle.'
    }
    $normalMoveBounds = [System.Drawing.Rectangle]$normalBoundsMethod.Invoke(
        $canvas, [object[]]@($rawBulb, [single]1.0))
    if (-not $normalMoveBounds.Contains($topHandle)) {
        throw "Normal light movement repaint bounds omit the rotation handle: $normalMoveBounds"
    }

    Write-Host "0-degree repaint bounds: $atZero"
    Write-Host "90-degree repaint bounds: $atNinety"
    Write-Host "Normal light move bounds: $normalMoveBounds"
    Write-Host 'PASS: rotation and normal light movement both repaint the complete stem and handle.'
}
finally {
    if ($null -ne $canvas.Image) { $canvas.Image.Dispose() }
    $canvas.Dispose()
}
