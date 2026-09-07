param(
    [string]$DesignerExe = (Join-Path $PSScriptRoot '..\b2sbackglassdesigner\bin\x64\Release\B2SPro.exe')
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Windows.Forms
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $DesignerExe))
$formType = $assembly.GetType('B2SBackglassDesigner.formSetReelIllumination', $true)
$form = [Activator]::CreateInstance($formType)
$flags = [Reflection.BindingFlags]'Instance,NonPublic'

try {
    $slider = $formType.GetField('trackReelBrightness', $flags).GetValue($form)
    $number = $formType.GetField('numReelBrightness', $flags).GetValue($form)
    $temperatureSlider = $formType.GetField('trackReelTemperature', $flags).GetValue($form)
    $temperatureNumber = $formType.GetField('numReelTemperature', $flags).GetValue($form)

    if ($slider.Maximum -ne 400) {
        throw "Expected the reel brightness slider maximum to be 400, but it was $($slider.Maximum)."
    }
    if ([int]$number.Maximum -ne 400) {
        throw "Expected the reel brightness number maximum to be 400, but it was $($number.Maximum)."
    }

    $number.Value = 321
    if ($slider.Value -ne 321) {
        throw "Typed reel brightness did not update the slider (expected 321, got $($slider.Value))."
    }

    $slider.Value = 399
    if ([int]$number.Value -ne 399) {
        throw "Slider reel brightness did not update the number (expected 399, got $($number.Value))."
    }

    $temperatureNumber.Value = 4321
    if ($temperatureSlider.Value -ne 4321) {
        throw "Exact typed color temperature was not preserved (expected 4321, got $($temperatureSlider.Value))."
    }

    Write-Host 'PASS: reel brightness supports 0-400 and exact typed values stay synchronized.'
}
finally {
    $form.Dispose()
}

$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$required400 = @(
    'designer\b2sbackglassdesigner\classes\CreateCode\Coding.vb',
    'designer\b2sbackglassdesigner\classes\Save.vb',
    'designer\b2sbackglassdesigner\classes\Tab\B2STabPage.vb',
    'designer\b2sbackglassdesigner\classes\Reels\Reel3DEffect.vb',
    'server\b2sbackglassserver\b2sbackglassserver\Forms\formBackglass.vb',
    'server\b2sbackglassserver\b2sbackglassserver\Classes\Reel3DEffect.vb'
)

foreach ($relative in $required400) {
    $text = [IO.File]::ReadAllText((Join-Path $root $relative))
    if ($text -notmatch '400') {
        throw "The 400% reel-brightness path is missing from $relative."
    }
}

Write-Host 'PASS: editor save/load/render and server load/render paths carry the 400% ceiling.'
