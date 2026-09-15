[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ModernTesterPath,
    [Parameter(Mandatory = $true)][string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if (-not (Test-Path -LiteralPath $ModernTesterPath -PathType Leaf)) { throw 'Modern ID Tester asset is missing.' }
if (Test-Path -LiteralPath $OutputPath) { throw 'Refusing to overwrite an existing legacy ID Tester.' }
if ([IO.Path]::GetExtension($OutputPath) -ine '.directb2s') { throw 'Legacy tester output must be .directb2s.' }

$document = [xml](Get-Content -LiteralPath $ModernTesterPath -Raw)
$embedded = $document.DocumentElement.SelectSingleNode('B2SProDesignerData')
if ($null -eq $embedded) { throw 'Modern ID Tester has no embedded designer data.' }
$project = [xml][Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($embedded.InnerText))
$runtimeParent = $document.DocumentElement.SelectSingleNode('Illumination')
$projectParent = $project.DocumentElement.SelectSingleNode('Illumination')
foreach ($parent in @($runtimeParent, $projectParent)) {
    foreach ($bulb in @($parent.SelectNodes('Bulb[@RomIDType="5"]'))) { $parent.RemoveChild($bulb) | Out-Null }
    if (@($parent.SelectNodes('Bulb')).Count -ne 460) { throw 'Legacy tester must have 460 supported indicators.' }
    foreach ($spec in @(@(1,350), @(2,100), @(3,10))) {
        $ids = @($parent.SelectNodes("Bulb[@RomIDType='$($spec[0])']") |
            ForEach-Object { [int]$_.GetAttribute('RomID') } | Sort-Object)
        if (($ids -join ',') -ne ((1..$spec[1]) -join ',')) {
            throw "Legacy ID set $($spec[0]) is not complete."
        }
    }
}

$backgroundNode = $document.DocumentElement.SelectSingleNode('Images/BackglassImage')
$imageBytes = [Convert]::FromBase64String($backgroundNode.GetAttribute('Value'))
$input = [IO.MemoryStream]::new($imageBytes)
$modern = [Drawing.Image]::FromStream($input)
if ($modern.Width -ne 3000 -or $modern.Height -ne 3000) { throw 'Modern tester background size changed.' }
$legacy = [Drawing.Bitmap]::new(3000, 2595, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [Drawing.Graphics]::FromImage($legacy)
$graphics.DrawImage($modern, [Drawing.Rectangle]::new(0,0,3000,2265),
    [Drawing.Rectangle]::new(0,0,3000,2265), [Drawing.GraphicsUnit]::Pixel)
$graphics.DrawImage($modern, [Drawing.Rectangle]::new(0,2265,3000,330),
    [Drawing.Rectangle]::new(0,2670,3000,330), [Drawing.GraphicsUnit]::Pixel)
$graphics.Dispose(); $modern.Dispose(); $input.Dispose()
$output = [IO.MemoryStream]::new()
$legacy.Save($output, [Drawing.Imaging.ImageFormat]::Png)
$background64 = [Convert]::ToBase64String($output.ToArray())
$output.Dispose()
$backgroundNode.SetAttribute('Value', $background64)
$backgroundNode.SetAttribute('FileName', 'B2S Pro ID Tester - Legacy.png')
$projectImage = $project.DocumentElement.SelectSingleNode('Images/BackgroundImages/MainImage')
$projectImage.SetAttribute('Image', $background64)
$projectImage.SetAttribute('FileName', 'B2S Pro ID Tester - Legacy.png')

$thumb = [Drawing.Bitmap]::new(300, 260)
$thumbGraphics = [Drawing.Graphics]::FromImage($thumb)
$thumbGraphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$thumbGraphics.DrawImage($legacy, 0, 0, 300, 260)
$thumbGraphics.Dispose(); $legacy.Dispose()
$thumbStream = [IO.MemoryStream]::new()
$thumb.Save($thumbStream, [Drawing.Imaging.ImageFormat]::Png)
$thumb64 = [Convert]::ToBase64String($thumbStream.ToArray())
$thumbStream.Dispose(); $thumb.Dispose()
$document.DocumentElement.SelectSingleNode('Images/ThumbnailImage').SetAttribute('Value', $thumb64)
$project.DocumentElement.SelectSingleNode('Images/ThumbnailImages/MainImage').SetAttribute('Image', $thumb64)
$document.DocumentElement.SelectSingleNode('VSName').SetAttribute('Value', 'B2S Pro ID Tester - Legacy')
$project.DocumentElement.SelectSingleNode('VSName').SetAttribute('Value', 'B2S Pro ID Tester - Legacy')
$project.DocumentElement.SelectSingleNode('Name').SetAttribute('Value', 'B2S Pro ID Tester - Legacy')
$embedded.InnerText = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($project.OuterXml))
$document.Save($OutputPath)
Write-Output "Created legacy ID Tester: $OutputPath"
