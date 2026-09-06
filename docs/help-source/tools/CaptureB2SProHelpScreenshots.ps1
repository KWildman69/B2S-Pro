param(
    [Parameter(Mandatory = $true)]
    [string]$WorkspaceRoot,
    [switch]$PivotOnly
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$outputFolder = Join-Path $WorkspaceRoot 'B2SProHelp-Source\pro-images\current-ui'
[IO.Directory]::CreateDirectory($outputFolder) | Out-Null

$exePath = Join-Path $WorkspaceRoot 'B2SBackglassDesigner-source\b2sbackglassdesigner\bin\x64\Release\B2SPro.exe'
$assembly = [Reflection.Assembly]::LoadFile($exePath)

[Windows.Forms.Application]::EnableVisualStyles()

function New-AppTypeInstance {
    param(
        [Parameter(Mandatory = $true)][string]$TypeName,
        [object[]]$Arguments = @()
    )

    $type = $assembly.GetType("B2SBackglassDesigner.$TypeName", $true)
    return [Activator]::CreateInstance($type, $Arguments)
}

function Prepare-Form {
    param([Parameter(Mandatory = $true)][Windows.Forms.Form]$Form)

    $Form.ShowInTaskbar = $false
    $Form.StartPosition = [Windows.Forms.FormStartPosition]::Manual
    $Form.Location = [Drawing.Point]::new(-20000, -20000)
    $Form.Show()
    [Windows.Forms.Application]::DoEvents()
    $Form.PerformLayout()
    [Windows.Forms.Application]::DoEvents()
}

function Save-FormImage {
    param(
        [Parameter(Mandatory = $true)][Windows.Forms.Form]$Form,
        [Parameter(Mandatory = $true)][string]$FileName
    )

    $width = [Math]::Max(1, $Form.Width)
    $height = [Math]::Max(1, $Form.Height)
    $bitmap = [Drawing.Bitmap]::new($width, $height, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $Form.DrawToBitmap($bitmap, [Drawing.Rectangle]::new(0, 0, $width, $height))
        $bitmap.Save((Join-Path $outputFolder $FileName), [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }
}

function Find-ControlByType {
    param(
        [Parameter(Mandatory = $true)][Windows.Forms.Control]$Root,
        [Parameter(Mandatory = $true)][Type]$ControlType
    )

    foreach ($control in $Root.Controls) {
        if ($ControlType.IsInstanceOfType($control)) { return $control }
        $nested = Find-ControlByType -Root $control -ControlType $ControlType
        if ($null -ne $nested) { return $nested }
    }
    return $null
}

function Close-Form {
    param([Parameter(Mandatory = $true)][Windows.Forms.Form]$Form)
    $Form.Close()
    $Form.Dispose()
    [Windows.Forms.Application]::DoEvents()
}

function New-DemoBulb {
    param(
        [Parameter(Mandatory = $true)][Drawing.Image]$Image,
        [string]$Name = 'Demo Snippet',
        [Drawing.Point]$Location = [Drawing.Point]::new(520, 1380),
        [Drawing.Size]$Size = [Drawing.Size]::new(126, 112)
    )

    $bulb = New-AppTypeInstance -TypeName 'Illumination.BulbInfo'
    $bulb.Name = $Name
    $bulb.ID = 1
    $bulb.Location = $Location
    $bulb.Size = $Size
    $bulb.IsImageSnippit = $true
    $bulb.Image = [Drawing.Bitmap]::new($Image)
    return $bulb
}

function New-PivotFlipperImage {
    $bitmap = [Drawing.Bitmap]::new(720, 220, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $bodyPath = [Drawing.Drawing2D.GraphicsPath]::new()
    $bodyBrush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255, 248, 225))
    $outlinePen = [Drawing.Pen]::new([Drawing.Color]::FromArgb(200, 20, 35), 10)
    $highlightPen = [Drawing.Pen]::new([Drawing.Color]::FromArgb(255, 255, 255), 5)
    $hubBrush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(205, 24, 38))
    $hubRingPen = [Drawing.Pen]::new([Drawing.Color]::FromArgb(255, 245, 220), 8)
    try {
        $graphics.Clear([Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        $bodyPath.StartFigure()
        $bodyPath.AddBezier(110, 42, 245, 44, 520, 65, 650, 83)
        $bodyPath.AddBezier(650, 83, 684, 88, 684, 132, 650, 137)
        $bodyPath.AddBezier(650, 137, 520, 155, 245, 176, 110, 178)
        $bodyPath.AddBezier(110, 178, 66, 178, 40, 151, 40, 110)
        $bodyPath.AddBezier(40, 110, 40, 69, 66, 42, 110, 42)
        $bodyPath.CloseFigure()

        $graphics.FillPath($bodyBrush, $bodyPath)
        $graphics.DrawPath($outlinePen, $bodyPath)
        $graphics.DrawBezier($highlightPen, 145, 61, 290, 63, 500, 79, 627, 96)
        $graphics.FillEllipse($hubBrush, 48, 48, 124, 124)
        $graphics.DrawEllipse($hubRingPen, 59, 59, 102, 102)
    }
    finally {
        $hubRingPen.Dispose()
        $hubBrush.Dispose()
        $highlightPen.Dispose()
        $outlinePen.Dispose()
        $bodyBrush.Dispose()
        $bodyPath.Dispose()
        $graphics.Dispose()
    }
    return $bitmap
}

function New-PivotDemoBulb {
    $flipperImage = New-PivotFlipperImage
    try {
        $pivotBulb = New-DemoBulb -Image $flipperImage -Name 'Flipper' -Location ([Drawing.Point]::new(900, 900)) -Size ([Drawing.Size]::new(540, 165))
    }
    finally {
        $flipperImage.Dispose()
    }
    $pivotBulb.SnippitInfo.PivotAnimationEnabled = $true
    $pivotBulb.SnippitInfo.PivotX = 0.15
    $pivotBulb.SnippitInfo.PivotY = 0.5
    $pivotBulb.SnippitInfo.PivotTipX = 0.9
    $pivotBulb.SnippitInfo.PivotTipY = 0.5
    $pivotBulb.SnippitInfo.PivotUpAngle = -35.0
    $pivotBulb.SnippitInfo.PivotTriggerID = 5
    return $pivotBulb
}

if ($PivotOnly) {
    $pivotBulb = New-PivotDemoBulb
    $pivot = New-AppTypeInstance -TypeName 'formPivotAnimation' -Arguments @($pivotBulb)
    Prepare-Form $pivot
    Save-FormImage $pivot 'pivot-animation.png'
    Close-Form $pivot
    Get-Item -LiteralPath (Join-Path $outputFolder 'pivot-animation.png') | Select-Object Name, Length
    return
}

$backgroundPath = Join-Path $WorkspaceRoot 'Projects\Batman (Data East 1991)\My Resources\Batman final.png'
$snippetPath = Join-Path $WorkspaceRoot 'Projects\Batman (Data East 1991)\My Resources\second trough template.png'
$background = [Drawing.Image]::FromFile($backgroundPath)
$snippetImage = [Drawing.Image]::FromFile($snippetPath)

try {
    # Give the light editors a real current tab so their own production preview
    # renderer can draw the complete backglass instead of a documentation mockup.
    $data = New-AppTypeInstance -TypeName 'Backglass+Data'
    $data.Image = [Drawing.Bitmap]::new($background)
    $tab = New-AppTypeInstance -TypeName 'B2STabPage' -Arguments @($data)
    $backglassType = $assembly.GetType('B2SBackglassDesigner.Backglass', $true)
    $backglassType.GetProperty('currentData').SetValue($null, $data, $null)
    $backglassType.GetProperty('currentTabPage').SetValue($null, $tab, $null)

    $pictureImport = New-AppTypeInstance -TypeName 'formPictureSequenceImport'
    Prepare-Form $pictureImport
    Save-FormImage $pictureImport 'picture-animation.png'
    Close-Form $pictureImport

    $snippetSettings = New-AppTypeInstance -TypeName 'formSnippitSettings'
    Prepare-Form $snippetSettings
    $snippetSettings.Opacity = 1.0
    Save-FormImage $snippetSettings 'snippet-settings.png'
    Close-Form $snippetSettings

    $motionBulb = New-DemoBulb -Image $snippetImage -Name 'Rolling Ball' -Location ([Drawing.Point]::new(350, 1510)) -Size ([Drawing.Size]::new(126, 112))
    $motionBulb.SnippitInfo.MotionPathPoints.Add([Drawing.PointF]::new(410.0, 1560.0))
    $motionBulb.SnippitInfo.MotionPathPoints.Add([Drawing.PointF]::new(720.0, 1380.0))
    $motionBulb.SnippitInfo.MotionPathPoints.Add([Drawing.PointF]::new(1160.0, 1160.0))
    $motionBulb.SnippitInfo.MotionPathPoints.Add([Drawing.PointF]::new(1640.0, 1290.0))
    $motionBulb.SnippitInfo.MotionPathRollEnabled = $true
    $motionBulb.SnippitInfo.MotionPathQueueTriggers = $true
    $motionBulb.SnippitInfo.MotionPathRespawnEnabled = $true
    $motionBulb.SnippitInfo.MotionPathRespawnPoint = [Drawing.PointF]::new(220.0, 1560.0)

    $motionPath = New-AppTypeInstance -TypeName 'formMotionPathTest' -Arguments @($motionBulb, $background, $false)
    Prepare-Form $motionPath
    Save-FormImage $motionPath 'motion-path.png'
    Close-Form $motionPath

    $trough = New-AppTypeInstance -TypeName 'formTroughWizard' -Arguments @($motionBulb, $background, $null)
    Prepare-Form $trough
    Save-FormImage $trough 'trough-animation.png'
    Close-Form $trough

    $pivotBulb = New-PivotDemoBulb
    $pivot = New-AppTypeInstance -TypeName 'formPivotAnimation' -Arguments @($pivotBulb)
    Prepare-Form $pivot
    Save-FormImage $pivot 'pivot-animation.png'
    Close-Form $pivot

    $ball = New-DemoBulb -Image $snippetImage -Name 'Physics Ball' -Location ([Drawing.Point]::new(550, 1450)) -Size ([Drawing.Size]::new(112, 112))
    $ball.SnippitInfo.PhysicsBall = $true
    $ball.SnippitInfo.MotionPathRollEnabled = $true
    $ball.SnippitInfo.PhysicsLauncherEnabled = $true
    $ball.SnippitInfo.PhysicsLauncherTriggerType = 1
    $ball.SnippitInfo.PhysicsLauncherTriggerID = 3
    $ball.SnippitInfo.PhysicsLauncherX = 606.0
    $ball.SnippitInfo.PhysicsLauncherY = 1506.0
    $ball.SnippitInfo.PhysicsLauncherAngle = -72.0
    $ball.SnippitInfo.PhysicsLauncherStrength = 900.0
    $ball.SnippitInfo.PhysicsLauncherCaptureRadius = 65.0

    $boundaryType = [Collections.Generic.List[Drawing.PointF]]
    $boundary = [Activator]::CreateInstance($boundaryType)
    $boundary.Add([Drawing.PointF]::new(250.0, 1650.0))
    $boundary.Add([Drawing.PointF]::new(620.0, 1500.0))
    $boundary.Add([Drawing.PointF]::new(1120.0, 1550.0))
    $boundary.Add([Drawing.PointF]::new(1700.0, 1370.0))
    $ball.SnippitInfo.PhysicsBoundaryPaths.Add($boundary)
    $ball.SnippitInfo.PhysicsBoundaryNames.Add('Lower Playfield')
    $ball.SnippitInfo.PhysicsBoundaryLocks.Add($true)

    $snippetArray = [Array]::CreateInstance($motionBulb.GetType(), 2)
    $snippetArray.SetValue($ball, 0)
    $snippetArray.SetValue($pivotBulb, 1)
    $physics = New-AppTypeInstance -TypeName 'formPhysicsEditor' -Arguments @($ball, $background, $snippetArray)
    Prepare-Form $physics
    $tabs = Find-ControlByType -Root $physics -ControlType ([Windows.Forms.TabControl])
    $tabs.SelectedIndex = 1
    [Windows.Forms.Application]::DoEvents()
    Save-FormImage $physics 'physics-boundaries.png'
    $tabs.SelectedIndex = 3
    [Windows.Forms.Application]::DoEvents()
    Save-FormImage $physics 'physics-launcher.png'
    Close-Form $physics

    $maskBulb = New-DemoBulb -Image $snippetImage -Name 'Masked Flasher' -Location ([Drawing.Point]::new(980, 330)) -Size ([Drawing.Size]::new(260, 220))
    $quickSelection = New-AppTypeInstance -TypeName 'formQuickSelection' -Arguments @($maskBulb, $background)
    Prepare-Form $quickSelection
    Save-FormImage $quickSelection 'quick-selection.png'
    Close-Form $quickSelection

    $flasher = New-DemoBulb -Image $snippetImage -Name 'Flasher 30' -Location ([Drawing.Point]::new(1420, 430)) -Size ([Drawing.Size]::new(310, 260))
    $flasher.IsImageSnippit = $false
    $flasher.IlluMode = [Enum]::Parse($flasher.IlluMode.GetType(), 'Flasher')
    $flasher.LightPurpose = [Enum]::Parse($flasher.LightPurpose.GetType(), 'Flasher')
    $flasher.GlowSpread = 100
    $flasher.GlowSoftness = 100
    $flasher.GlowFalloff = 100
    $flasher.GlowIntensity = 925
    $flasher.ArtworkContrast = 55
    $flasher.LightTemperature = 6500
    $flasher.MaskRadius = 58
    $flasher.MaskSmooth = 100
    $flasher.MaskFeather = 216
    $flasher.MaskContrast = 1
    $flasher.MaskShiftEdge = -10
    $flasher.FlasherRadialSpikes = 55
    $data.Bulbs.Add($flasher)
    $flasherEditor = New-AppTypeInstance -TypeName 'formLightDiffusionEditor' -Arguments @($flasher, $null)
    Prepare-Form $flasherEditor
    Start-Sleep -Milliseconds 180
    [Windows.Forms.Application]::DoEvents()
    Save-FormImage $flasherEditor 'flasher-editor.png'
    $flasherEditor.Dispose()

    $lamp = New-DemoBulb -Image $snippetImage -Name 'Lamp 12' -Location ([Drawing.Point]::new(720, 520)) -Size ([Drawing.Size]::new(260, 220))
    $lamp.IsImageSnippit = $false
    $lamp.GlowSpread = 70
    $lamp.GlowSoftness = 70
    $lamp.GlowFalloff = 75
    $lamp.GlowIntensity = 380
    $lamp.LightTemperature = 4300
    $data.Bulbs.Add($lamp)
    $lampEditor = New-AppTypeInstance -TypeName 'formLightDiffusionEditor' -Arguments @($lamp, $null)
    Prepare-Form $lampEditor
    Start-Sleep -Milliseconds 180
    [Windows.Forms.Application]::DoEvents()
    Save-FormImage $lampEditor 'lamp-editor.png'
    $lampEditor.Dispose()

    $reels = New-AppTypeInstance -TypeName 'formToolReelsAndLEDs'
    Prepare-Form $reels
    Save-FormImage $reels 'reels-and-leds.png'
    # This tool window normally hides rather than closes. Dispose it directly
    # in the off-screen documentation renderer so its production close logic
    # cannot keep the capture process alive.
    $reels.Dispose()
    [Windows.Forms.Application]::DoEvents()

    # Draw the score editor example from the exact current editor geometry:
    # orange rotated frame, two perspective handles, center depth handle, and
    # the orange rotation stem/handle 24 pixels above the selected display.
    $scoreWidth = 1100
    $scoreHeight = 520
    $scoreBitmap = [Drawing.Bitmap]::new($scoreWidth, $scoreHeight, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($scoreBitmap)
    try {
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.Clear([Drawing.Color]::FromArgb(7, 10, 18))
        $graphics.DrawImage($background, [Drawing.Rectangle]::new(0, 0, $scoreWidth, $scoreHeight),
                            [Drawing.Rectangle]::new(240, 1030, 2020, 780), [Drawing.GraphicsUnit]::Pixel)
        $graphics.FillRectangle([Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(155, 0, 0, 0)), 0, 0, $scoreWidth, $scoreHeight)

        $rect = [Drawing.RectangleF]::new(210.0, 205.0, 680.0, 116.0)
        $centerX = $rect.X + $rect.Width / 2.0
        $centerY = $rect.Y + $rect.Height / 2.0
        $angle = -9.0
        $state = $graphics.Save()
        $graphics.TranslateTransform($centerX, $centerY)
        $graphics.RotateTransform($angle)
        $graphics.TranslateTransform(-$centerX, -$centerY)
        $graphics.FillRectangle([Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(225, 8, 5, 4)), $rect)

        $digitFolder = Join-Path $WorkspaceRoot 'B2SBackglassDesigner-source\b2sbackglassdesigner\Images'
        $digits = '12345678'.ToCharArray()
        $digitX = $rect.X + 12
        foreach ($digit in $digits) {
            $digitImage = [Drawing.Image]::FromFile((Join-Path $digitFolder ("LED_$digit.png")))
            try {
                $graphics.DrawImage($digitImage, [Drawing.RectangleF]::new($digitX, $rect.Y + 10, 70, 96))
            }
            finally {
                $digitImage.Dispose()
            }
            $digitX += 82
        }

        $orangePen = [Drawing.Pen]::new([Drawing.Color]::DarkOrange, 2.0)
        $graphics.DrawRectangle($orangePen, $rect.X, $rect.Y, $rect.Width, $rect.Height)
        $graphics.DrawRectangle($orangePen, $rect.X + 2, $rect.Y + 2, $rect.Width - 4, $rect.Height - 4)
        $graphics.FillRectangle([Drawing.Brushes]::White, $rect.Right - 18, $rect.Y + 7, 11, 11)
        $graphics.DrawLine([Drawing.Pens]::Black, $rect.Right - 16, $rect.Y + 9, $rect.Right - 9, $rect.Y + 16)
        $graphics.DrawLine([Drawing.Pens]::Black, $rect.Right - 9, $rect.Y + 9, $rect.Right - 16, $rect.Y + 16)
        $graphics.Restore($state)

        $radians = $angle * [Math]::PI / 180.0
        function Rotate-Point([double]$x, [double]$y) {
            $dx = $x - $centerX
            $dy = $y - $centerY
            return [Drawing.PointF]::new([single]($centerX + $dx * [Math]::Cos($radians) - $dy * [Math]::Sin($radians)),
                                         [single]($centerY + $dx * [Math]::Sin($radians) + $dy * [Math]::Cos($radians)))
        }
        $topLeft = Rotate-Point $rect.Left $rect.Top
        $topRight = Rotate-Point $rect.Right $rect.Top
        $bottomRight = Rotate-Point $rect.Right $rect.Bottom
        $bottomLeft = Rotate-Point $rect.Left $rect.Bottom
        $quad = [Drawing.PointF[]]@($topLeft, $topRight, $bottomRight, $bottomLeft)
        $skyPen = [Drawing.Pen]::new([Drawing.Color]::DeepSkyBlue, 2.0)
        $graphics.DrawPolygon($skyPen, $quad)

        foreach ($item in @(@($topLeft, [Drawing.Color]::LimeGreen), @($topRight, [Drawing.Color]::DeepSkyBlue))) {
            $point = $item[0]
            $handlePen = [Drawing.Pen]::new($item[1], 2.0)
            $graphics.FillEllipse([Drawing.Brushes]::White, $point.X - 7, $point.Y - 7, 14, 14)
            $graphics.DrawEllipse($handlePen, $point.X - 7, $point.Y - 7, 14, 14)
            $graphics.DrawLine($handlePen, $point.X, $point.Y - 4, $point.X, $point.Y + 4)
            $handlePen.Dispose()
        }

        $graphics.FillEllipse([Drawing.Brushes]::White, $centerX - 8, $centerY - 8, 16, 16)
        $graphics.DrawEllipse($skyPen, $centerX - 8, $centerY - 8, 16, 16)
        $graphics.DrawLine($skyPen, $centerX - 5, $centerY, $centerX + 5, $centerY)

        $handleRadius = $rect.Height / 2.0 + 24.0
        $handleX = $centerX + [Math]::Sin($radians) * $handleRadius
        $handleY = $centerY - [Math]::Cos($radians) * $handleRadius
        $topMidX = ($topLeft.X + $topRight.X) / 2.0
        $topMidY = ($topLeft.Y + $topRight.Y) / 2.0
        $graphics.DrawLine($orangePen, [single]$topMidX, [single]$topMidY, [single]$handleX, [single]$handleY)
        $graphics.FillEllipse([Drawing.Brushes]::White, $handleX - 7, $handleY - 7, 14, 14)
        $graphics.DrawEllipse($orangePen, $handleX - 7, $handleY - 7, 14, 14)

        $titleFont = [Drawing.Font]::new('Tahoma', 22, [Drawing.FontStyle]::Bold)
        $bodyFont = [Drawing.Font]::new('Tahoma', 12, [Drawing.FontStyle]::Regular)
        $graphics.DrawString('Selected score display', $titleFont, [Drawing.Brushes]::White, 28, 24)
        $graphics.DrawString('Drag the orange handle to rotate. The top handles change perspective; the center handle changes depth.',
                             $bodyFont, [Drawing.Brushes]::White, 30, 62)
        $titleFont.Dispose()
        $bodyFont.Dispose()
        $orangePen.Dispose()
        $skyPen.Dispose()
    }
    finally {
        $graphics.Dispose()
    }
    try {
        $scoreBitmap.Save((Join-Path $outputFolder 'score-rotation-handles.png'), [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $scoreBitmap.Dispose()
    }

    $saveBitmap = [Drawing.Bitmap]::new(1100, 360, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $saveGraphics = [Drawing.Graphics]::FromImage($saveBitmap)
    try {
        $saveGraphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $saveGraphics.Clear([Drawing.Color]::FromArgb(7, 12, 20))
        $titleFont = [Drawing.Font]::new('Tahoma', 24, [Drawing.FontStyle]::Bold)
        $stepFont = [Drawing.Font]::new('Tahoma', 15, [Drawing.FontStyle]::Bold)
        $smallFont = [Drawing.Font]::new('Tahoma', 11, [Drawing.FontStyle]::Regular)
        $saveGraphics.DrawString('Save, create, and test', $titleFont, [Drawing.Brushes]::White, 30, 24)

        $boxes = @(
            @{ X = 35;  Color = [Drawing.Color]::FromArgb(30, 83, 135); Title = '1. Save Project'; Detail = 'Keep the editable .b2s source' },
            @{ X = 385; Color = [Drawing.Color]::FromArgb(22, 111, 139); Title = '2. Create DirectB2S'; Detail = 'Build the finished backglass file' },
            @{ X = 735; Color = [Drawing.Color]::FromArgb(47, 119, 56); Title = '3. Preview & Test'; Detail = 'Verify the exported behavior' }
        )
        foreach ($box in $boxes) {
            $brush = [Drawing.SolidBrush]::new($box.Color)
            $border = [Drawing.Pen]::new([Drawing.Color]::FromArgb(112, 208, 255), 2.0)
            $saveGraphics.FillRectangle($brush, $box.X, 100, 300, 105)
            $saveGraphics.DrawRectangle($border, $box.X, 100, 300, 105)
            $saveGraphics.DrawString($box.Title, $stepFont, [Drawing.Brushes]::White, $box.X + 17, 121)
            $saveGraphics.DrawString($box.Detail, $smallFont, [Drawing.Brushes]::White, $box.X + 17, 161)
            $brush.Dispose()
            $border.Dispose()
        }
        $arrowPen = [Drawing.Pen]::new([Drawing.Color]::DeepSkyBlue, 5.0)
        $arrowPen.EndCap = [Drawing.Drawing2D.LineCap]::ArrowAnchor
        $saveGraphics.DrawLine($arrowPen, 341, 152, 375, 152)
        $saveGraphics.DrawLine($arrowPen, 691, 152, 725, 152)

        $compatBrush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(28, 70, 48))
        $compatPen = [Drawing.Pen]::new([Drawing.Color]::FromArgb(66, 226, 140), 2.0)
        $saveGraphics.FillRectangle($compatBrush, 35, 240, 1000, 76)
        $saveGraphics.DrawRectangle($compatPen, 35, 240, 1000, 76)
        $saveGraphics.DrawString('Legacy-safe export', $stepFont, [Drawing.Brushes]::White, 55, 254)
        $saveGraphics.DrawString('New feature metadata activates the B2S Pro runtime path; older files continue through the established legacy path.',
                                 $smallFont, [Drawing.Brushes]::White, 55, 284)
        $titleFont.Dispose()
        $stepFont.Dispose()
        $smallFont.Dispose()
        $arrowPen.Dispose()
        $compatBrush.Dispose()
        $compatPen.Dispose()
    }
    finally {
        $saveGraphics.Dispose()
    }
    try {
        $saveBitmap.Save((Join-Path $outputFolder 'save-export-test.png'), [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $saveBitmap.Dispose()
    }

    $tab.Dispose()
    $data.Image.Dispose()
}
finally {
    $background.Dispose()
    $snippetImage.Dispose()
}

Get-ChildItem -LiteralPath $outputFolder -File -Filter '*.png' |
    Sort-Object Name |
    Select-Object Name, Length
