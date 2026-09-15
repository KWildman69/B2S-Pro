[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Core') {
    throw 'Build-CurrentRelease.ps1 must be run with PowerShell 7 (pwsh) so ZIP entry paths use installer-compatible forward slashes.'
}
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$workspaceRoot = (Resolve-Path -LiteralPath (Join-Path $repositoryRoot '..')).Path
$localWorkRoot = Join-Path $workspaceRoot 'B2S-Local-Work'
$baselineRoot = Join-Path $localWorkRoot 'Package-Templates'
$localToolsRoot = Join-Path $localWorkRoot 'Build-Tools'
$currentReleaseRoot = Join-Path $localWorkRoot 'Current-Release'
$publicReleaseRoot = Join-Path $currentReleaseRoot 'Public'
$privateReleaseRoot = Join-Path $currentReleaseRoot 'Private-Tester'
$handoffRoot = Join-Path $currentReleaseRoot 'Local-Handoff'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$buildStagingRoot = Join-Path $localWorkRoot 'Build-Staging'
$buildRoot = Join-Path $buildStagingRoot ('release-' + $stamp)
$sourceCopyRoot = Join-Path $buildRoot 'source-copy'
$designerRoot = Join-Path $sourceCopyRoot 'src\designer'
$serverRoot = Join-Path $sourceCopyRoot 'src\server'
$installerRoot = Join-Path $sourceCopyRoot 'installer'
$distRoot = Join-Path $buildRoot 'installer-dist'
$backupRoot = Join-Path (Join-Path $localWorkRoot 'Package-Backups') ($stamp + '-before-current-release')

function Assert-ChildPath([string]$path, [string]$parent, [string]$description) {
    $fullPath = [IO.Path]::GetFullPath($path)
    $fullParent = [IO.Path]::GetFullPath($parent).TrimEnd('\')
    if (-not $fullPath.StartsWith($fullParent + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe $description path: $fullPath"
    }
}

function Copy-DirectoryContents([string]$source, [string]$destination) {
    if (-not (Test-Path -LiteralPath $source -PathType Container)) {
        throw "Copy source is missing: $source"
    }
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Get-ChildItem -LiteralPath $source -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $destination -Recurse -Force
    }
}

function Reset-Directory([string]$path, [string]$allowedParent) {
    Assert-ChildPath $path $allowedParent 'directory reset'
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

function Remove-GeneratedDirectories([string]$root) {
    Assert-ChildPath $root $buildRoot 'generated-directory cleanup root'
    Get-ChildItem -LiteralPath $root -Recurse -Directory -Force |
        Where-Object { $_.Name -in @('bin', 'obj', '.vs', 'tests', 'dist') } |
        Sort-Object FullName -Descending |
        ForEach-Object {
            Assert-ChildPath $_.FullName $root 'generated directory'
            Remove-Item -LiteralPath $_.FullName -Recurse -Force
        }
}

function New-Zip([string]$sourceDirectory, [string]$destinationZip) {
    Assert-ChildPath $sourceDirectory $buildRoot 'ZIP source'
    Assert-ChildPath $destinationZip $localWorkRoot 'ZIP destination'
    if (Test-Path -LiteralPath $destinationZip) {
        Remove-Item -LiteralPath $destinationZip -Force
    }
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $sourceDirectory,
        $destinationZip,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false)
}

function Write-ShaSidecar([string]$zipPath, [string]$sidecarPath) {
    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
    Set-Content -LiteralPath $sidecarPath -Value ($hash + '  ' + [IO.Path]::GetFileName($zipPath)) -Encoding ascii
}

function Get-ZipEntryHash([System.IO.Compression.ZipArchive]$archive, [string]$entryName) {
    $entry = $archive.GetEntry($entryName)
    if ($null -eq $entry) { throw "ZIP entry is missing: $entryName" }
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $stream = $entry.Open()
        try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
        finally { $stream.Dispose() }
    }
    finally { $sha.Dispose() }
}

foreach ($path in @($repositoryRoot, $workspaceRoot, $localWorkRoot, $baselineRoot, $localToolsRoot)) {
    if (-not (Test-Path -LiteralPath $path -PathType Container)) {
        throw "Required release path is missing: $path"
    }
}
foreach ($path in @(
    (Join-Path $baselineRoot 'B2S-Pro-Server-3.0.0.zip'),
    (Join-Path $localToolsRoot 'htmlhelp2\hhc.exe')
)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required release baseline/tool is missing: $path"
    }
}

Assert-ChildPath $buildRoot $localWorkRoot 'build staging'
Assert-ChildPath $currentReleaseRoot $localWorkRoot 'current release'
Assert-ChildPath $backupRoot $localWorkRoot 'release backup'
New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null

if (Test-Path -LiteralPath $currentReleaseRoot -PathType Container) {
    New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
    Copy-DirectoryContents $currentReleaseRoot $backupRoot
}
Reset-Directory $currentReleaseRoot $localWorkRoot
New-Item -ItemType Directory -Path $publicReleaseRoot, $privateReleaseRoot, $handoffRoot, $sourceCopyRoot, $distRoot -Force | Out-Null

# Build from an external copy so the Git repository never receives bin, obj,
# compiled help, installer dist, package staging, or other generated output.
Copy-DirectoryContents (Join-Path $repositoryRoot 'src') (Join-Path $sourceCopyRoot 'src')
Copy-DirectoryContents (Join-Path $repositoryRoot 'docs') (Join-Path $sourceCopyRoot 'docs')
Copy-DirectoryContents (Join-Path $repositoryRoot 'installer') $installerRoot
foreach ($name in @('README.md', 'CHANGELOG.md', 'CREDITS.md', 'LICENSE.txt')) {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot $name) -Destination $sourceCopyRoot -Force
}
Remove-GeneratedDirectories $sourceCopyRoot

# Compile the current help only in the external source copy.
$helpSourceRoot = Join-Path $sourceCopyRoot 'docs\help-source'
$helpCompiler = Join-Path $localToolsRoot 'htmlhelp2\hhc.exe'
$compiledHelp = Join-Path $helpSourceRoot 'B2SProHelp.chm'
Push-Location $helpSourceRoot
try { & $helpCompiler 'B2SBackglassDesigner.hhp' | Out-Host }
finally { Pop-Location }
if (-not (Test-Path -LiteralPath $compiledHelp -PathType Leaf)) {
    throw 'B2S Pro help compilation did not produce B2SProHelp.chm.'
}
$helpErrorLog = Join-Path $helpSourceRoot '_errorlog.txt'
if (Test-Path -LiteralPath $helpErrorLog -PathType Leaf) { Remove-Item -LiteralPath $helpErrorLog -Force }
Copy-Item -LiteralPath $compiledHelp -Destination (Join-Path $designerRoot 'b2sbackglassdesigner\Resources\B2SProHelp.chm') -Force

# Build every distributed executable from the same external source snapshot.
$msBuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path -LiteralPath $msBuild -PathType Leaf)) { throw "MSBuild is missing: $msBuild" }
& $msBuild (Join-Path $designerRoot 'B2SBackglassDesigner.sln') /t:Rebuild /p:Configuration=Release /p:Platform=x64 /m:1 /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'x64 Designer build failed.' }
& $msBuild (Join-Path $designerRoot 'B2SBackglassDesigner.sln') /t:Rebuild /p:Configuration=Release /p:Platform=x86 /m:1 /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'x86 Designer build failed.' }
& $msBuild (Join-Path $serverRoot 'b2sbackglassserver\B2SBackglassServer.sln') /t:Rebuild /p:Configuration=Release /p:Platform='Any CPU' /m:1 /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'B2S Pro Server build failed.' }
& $msBuild (Join-Path $serverRoot 'b2sbackglassserverregisterapp\B2SBackglassServerRegisterApp.sln') /t:Rebuild /p:Configuration=Release /p:Platform=x64 /m:1 /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'x64 B2S Server registration application build failed.' }
& $msBuild (Join-Path $serverRoot 'b2sbackglassserverregisterapp\B2SBackglassServerRegisterApp.sln') /t:Rebuild /p:Configuration=Release /p:Platform=x86 /m:1 /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'x86 B2S Server registration application build failed.' }

$x64Designer = Join-Path $designerRoot 'b2sbackglassdesigner\bin\x64\Release\B2SPro.exe'
$x86Designer = Join-Path $designerRoot 'b2sbackglassdesigner\bin\x86\Release\B2SPro.exe'
$serverDll = Join-Path $serverRoot 'b2sbackglassserver\b2sbackglassserver\bin\Release\B2SBackglassServer.dll'
$serverExe = Join-Path $serverRoot 'b2sbackglassserver\B2SBackglassServer\bin\Release\B2SBackglassServerEXE.exe'
$registerApp = Join-Path $serverRoot 'b2sbackglassserverregisterapp\b2sbackglassserverregisterapp\bin\x64\Release\B2SBackglassServerRegisterApp.exe'
foreach ($path in @($x64Designer, $x86Designer, $serverDll, $serverExe, $registerApp)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Build output is missing: $path" }
}

# Compile both setup programs and the shared non-admin update checker before
# packaging so the exact checker binary can be embedded in both runtime ZIPs.
& (Join-Path $installerRoot 'build-installer.ps1') -OutputRoot $distRoot
$updateChecker = Join-Path $distRoot 'B2SUpdateChecker.exe'
foreach ($path in @(
    (Join-Path $distRoot 'B2SProSetup.exe'),
    (Join-Path $distRoot 'B2SServerSetup.exe'),
    (Join-Path $distRoot 'B2SSetup.SelfTest.exe'),
    $updateChecker
)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Installer build output is missing: $path" }
}

# Build the portable Designer runtime distribution used by both manual
# downloads and B2SProSetup.exe.
$designerRuntimeStage = Join-Path $buildRoot 'designer-distribution'
foreach ($platform in @('x64', 'x86')) {
    $platformStage = Join-Path $designerRuntimeStage $platform
    New-Item -ItemType Directory -Path $platformStage -Force | Out-Null
    foreach ($name in @('B2SPro.exe', 'B2SPro.exe.config')) {
        Copy-Item -LiteralPath (Join-Path $designerRoot "b2sbackglassdesigner\bin\$platform\Release\$name") -Destination $platformStage -Force
    }
    foreach ($name in @('B2SVPinMAMEStarter.exe', 'B2SVPinMAMEStarter.exe.config')) {
        Copy-Item -LiteralPath (Join-Path $designerRoot "B2SVPinMAMEStarter\bin\$platform\Release\$name") -Destination $platformStage -Force
    }
    Copy-Item -LiteralPath $updateChecker -Destination $platformStage -Force
}
foreach ($name in @('README.md', 'CHANGELOG.md', 'CREDITS.md', 'LICENSE.txt')) {
    Copy-Item -LiteralPath (Join-Path $sourceCopyRoot $name) -Destination $designerRuntimeStage -Force
}
$designerZip = Join-Path $buildRoot 'B2S-Pro-Backglass-1.0.1.zip'
$designerSidecar = $designerZip + '.sha256'
New-Zip $designerRuntimeStage $designerZip
Write-ShaSidecar $designerZip $designerSidecar

# Preserve unchanged public Server utilities from the baseline and replace all
# rebuilt Server binaries, including the registration utility.
$serverRuntimeStage = Join-Path $buildRoot 'server-runtime'
[System.IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $baselineRoot 'B2S-Pro-Server-3.0.0.zip'), $serverRuntimeStage)
$retiredServerPackageFiles = @(
    'ScreenRes.txt',
    'Changelog.txt',
    'B2S-native-rotation-changelog.txt',
    'B2SNativeRotationDiagnostic.log',
    'B2SNativeRotationDiagnostic.txt'
)
foreach ($name in $retiredServerPackageFiles) {
    $path = Join-Path $serverRuntimeStage $name
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        Remove-Item -LiteralPath $path -Force
    }
}
Copy-Item -LiteralPath $serverDll -Destination (Join-Path $serverRuntimeStage 'B2SBackglassServer.dll') -Force
Copy-Item -LiteralPath $serverExe -Destination (Join-Path $serverRuntimeStage 'B2SBackglassServerEXE.exe') -Force
Copy-Item -LiteralPath $registerApp -Destination (Join-Path $serverRuntimeStage 'B2SBackglassServerRegisterApp.exe') -Force
Copy-Item -LiteralPath $updateChecker -Destination (Join-Path $serverRuntimeStage 'B2SUpdateChecker.exe') -Force
Copy-Item -LiteralPath (Join-Path $sourceCopyRoot 'CHANGELOG.md') -Destination (Join-Path $serverRuntimeStage 'B2S-Pro-Changelog.md') -Force
Copy-Item -LiteralPath (Join-Path $serverRoot 'ScreenResTemplate.txt') -Destination (Join-Path $serverRuntimeStage 'ScreenResTemplate.txt') -Force

$serverZip = Join-Path $buildRoot 'B2S-Pro-Server-3.0.0.zip'
$serverSidecar = $serverZip + '.sha256'
New-Zip $serverRuntimeStage $serverZip
Write-ShaSidecar $serverZip $serverSidecar

# Stage public release assets and run the already-built installer sandbox
# self-test against the exact packages being shipped.
foreach ($path in @($designerZip, $designerSidecar, $serverZip, $serverSidecar)) {
    Copy-Item -LiteralPath $path -Destination $publicReleaseRoot -Force
    Copy-Item -LiteralPath $path -Destination $distRoot -Force
}
$selfTest = Join-Path $distRoot 'B2SSetup.SelfTest.exe'
& $selfTest --self-test (Join-Path $distRoot 'B2S-Pro-Backglass-1.0.1.zip') (Join-Path $distRoot 'B2S-Pro-Server-3.0.0.zip')
if ($LASTEXITCODE -ne 0) { throw "Installer self-test failed with exit code $LASTEXITCODE" }
Copy-Item -LiteralPath (Join-Path $distRoot 'B2SProSetup.exe') -Destination $publicReleaseRoot -Force
Copy-Item -LiteralPath (Join-Path $distRoot 'B2SServerSetup.exe') -Destination $publicReleaseRoot -Force

$publicNames = @(
    'B2SProSetup.exe',
    'B2SServerSetup.exe',
    'B2S-Pro-Backglass-1.0.1.zip',
    'B2S-Pro-Backglass-1.0.1.zip.sha256',
    'B2S-Pro-Server-3.0.0.zip',
    'B2S-Pro-Server-3.0.0.zip.sha256'
)
foreach ($name in $publicNames) {
    $path = Join-Path $publicReleaseRoot $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required public release asset is missing: $path" }
}
$unexpectedPublicAssets = @(Get-ChildItem -LiteralPath $publicReleaseRoot -File | Where-Object { $_.Name -notin $publicNames })
if ($unexpectedPublicAssets.Count -ne 0) {
    throw "Unexpected public release asset: $($unexpectedPublicAssets[0].Name)"
}
if (@(Get-ChildItem -LiteralPath $publicReleaseRoot -File).Count -ne $publicNames.Count) {
    throw 'The public release must contain exactly the six approved assets.'
}

# Create private/offline handoff packages outside Git.
$offlineProStage = Join-Path $buildRoot 'offline-pro'
$offlineServerStage = Join-Path $buildRoot 'offline-server'
New-Item -ItemType Directory -Path $offlineProStage, $offlineServerStage -Force | Out-Null
foreach ($name in @('B2SProSetup.exe', 'B2S-Pro-Backglass-1.0.1.zip', 'B2S-Pro-Backglass-1.0.1.zip.sha256', 'B2S-Pro-Server-3.0.0.zip', 'B2S-Pro-Server-3.0.0.zip.sha256')) {
    Copy-Item -LiteralPath (Join-Path $publicReleaseRoot $name) -Destination $offlineProStage -Force
}
foreach ($name in @('B2SServerSetup.exe', 'B2S-Pro-Server-3.0.0.zip', 'B2S-Pro-Server-3.0.0.zip.sha256')) {
    Copy-Item -LiteralPath (Join-Path $publicReleaseRoot $name) -Destination $offlineServerStage -Force
}
New-Zip $offlineProStage (Join-Path $privateReleaseRoot 'B2S-Pro-Offline-Setup-1.0.1.zip')
New-Zip $offlineProStage (Join-Path $privateReleaseRoot 'B2S-Pro-Private-Tester-Package-1.0.1.zip')
New-Zip $offlineServerStage (Join-Path $privateReleaseRoot 'B2S-Server-Offline-Setup-3.0.0.zip')
Copy-Item -LiteralPath $x64Designer -Destination $handoffRoot -Force
Copy-Item -LiteralPath $serverDll -Destination $handoffRoot -Force
Copy-Item -LiteralPath $serverExe -Destination $handoffRoot -Force
Copy-Item -LiteralPath $updateChecker -Destination $handoffRoot -Force

# Audit archive content and prove all runtime files came from this build.
$forbiddenPattern = '(^|/)(bin|obj|\.vs|tests|dist|B2SPro-Backups|Package-Backups|Install-Backups|recovery|diagnostics?)(/|$)|(^|/)[^/]+\.(log|tmp|bak|b2b|directb2s|b2spro)$|(^|/)[^/]*(audit|real-build-baseline)[^/]*$'
foreach ($zipPath in Get-ChildItem -LiteralPath $publicReleaseRoot, $privateReleaseRoot -Filter '*.zip' -File) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath.FullName)
    try {
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName.Contains('\')) { throw "Archive uses a backslash entry path: $($zipPath.Name): $($entry.FullName)" }
            if ($entry.FullName -match $forbiddenPattern -and
                $entry.FullName -notmatch '(^|/)Resources/IDTester\.B2SPro$') {
                throw "Forbidden local-only content in $($zipPath.Name): $($entry.FullName)"
            }
        }
    }
    finally { $archive.Dispose() }
}
$designerArchive = [System.IO.Compression.ZipFile]::OpenRead((Join-Path $publicReleaseRoot 'B2S-Pro-Backglass-1.0.1.zip'))
try {
    $designerRuntimeComparisons = @{
        'x64/B2SPro.exe' = $x64Designer
        'x86/B2SPro.exe' = $x86Designer
        'x64/B2SUpdateChecker.exe' = $updateChecker
        'x86/B2SUpdateChecker.exe' = $updateChecker
        'x64/B2SVPinMAMEStarter.exe' = (Join-Path $designerRoot 'B2SVPinMAMEStarter\bin\x64\Release\B2SVPinMAMEStarter.exe')
        'x86/B2SVPinMAMEStarter.exe' = (Join-Path $designerRoot 'B2SVPinMAMEStarter\bin\x86\Release\B2SVPinMAMEStarter.exe')
    }
    foreach ($entryName in $designerRuntimeComparisons.Keys) {
        if ((Get-ZipEntryHash $designerArchive $entryName) -ne (Get-FileHash -LiteralPath $designerRuntimeComparisons[$entryName] -Algorithm SHA256).Hash) {
            throw "Designer package runtime mismatch: $entryName"
        }
    }
    $designerSourceEntries = @($designerArchive.Entries | Where-Object {
        $_.FullName -match '\.(vb|cs|vbproj|csproj|sln|resx)$'
    })
    if ($designerSourceEntries.Count -ne 0) {
        throw "Designer runtime package contains source: $($designerSourceEntries[0].FullName)"
    }
}
finally { $designerArchive.Dispose() }
$serverArchive = [System.IO.Compression.ZipFile]::OpenRead((Join-Path $publicReleaseRoot 'B2S-Pro-Server-3.0.0.zip'))
try {
    $expectedServerEntries = @(
        'B2S-Pro-Changelog.md',
        'B2SBackglassServer.dll',
        'B2SBackglassServerEXE.exe',
        'B2SBackglassServerEXE.exe.config',
        'B2SBackglassServerRegisterApp.exe',
        'B2SInit.cmd',
        'B2SServerPluginInterface.dll',
        'B2SUpdateChecker.exe',
        'B2SWindowPunch.exe',
        'B2S_ScreenResIdentifier.exe',
        'B2S_ScreenResIdentifier.exe.config',
        'B2S_SetUp.exe',
        'B2S_SetUp.exe.config',
        'license.txt',
        'README.txt',
        'ScreenResTemplate.txt',
        'ScreenResTemplates.cmd',
        'B2STools/B2SRandom.cmd',
        'B2STools/B2STools.txt',
        'B2STools/directb2sReelSoundsONOFF.cmd',
        'B2STools/directb2sReelSoundsONOFF.xsl',
        'B2STools/DmdDeviceIniScale.cmd',
        'Plugins/Plugins.txt',
        'Plugins64/Plugins.txt',
        'ScreenResTemplates/ScreenResTemplates.txt'
    )
    $actualServerEntries = @($serverArchive.Entries | Where-Object {
        -not [String]::IsNullOrEmpty($_.Name)
    } | ForEach-Object { $_.FullName })
    $serverManifestDifference = @(Compare-Object -ReferenceObject $expectedServerEntries -DifferenceObject $actualServerEntries)
    if ($serverManifestDifference.Count -ne 0) {
        $firstDifference = $serverManifestDifference[0]
        throw "Server package manifest mismatch ($($firstDifference.SideIndicator)): $($firstDifference.InputObject)"
    }
    foreach ($entryName in $retiredServerPackageFiles) {
        if ($null -ne $serverArchive.GetEntry($entryName)) {
            throw "Server package contains retired file: $entryName"
        }
    }
    $serverChangelogs = @($serverArchive.Entries | Where-Object {
        -not [String]::IsNullOrEmpty($_.Name) -and $_.Name -match '(?i)changelog'
    })
    if ($serverChangelogs.Count -ne 1 -or $serverChangelogs[0].FullName -cne 'B2S-Pro-Changelog.md') {
        throw 'The Server package must contain only B2S-Pro-Changelog.md as its changelog.'
    }
    $serverRuntimeComparisons = @{
        'B2SBackglassServer.dll' = $serverDll
        'B2SBackglassServerEXE.exe' = $serverExe
        'B2SBackglassServerRegisterApp.exe' = $registerApp
        'B2SUpdateChecker.exe' = $updateChecker
    }
    foreach ($entryName in $serverRuntimeComparisons.Keys) {
        if ((Get-ZipEntryHash $serverArchive $entryName) -ne (Get-FileHash -LiteralPath $serverRuntimeComparisons[$entryName] -Algorithm SHA256).Hash) {
            throw "Server package runtime mismatch: $entryName"
        }
    }
}
finally { $serverArchive.Dispose() }

$repoGenerated = @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src'), (Join-Path $repositoryRoot 'installer') -Recurse -Directory -Force |
    Where-Object { $_.Name -in @('bin', 'obj', 'dist', '.vs') })
if ($repoGenerated.Count -ne 0) { throw 'Generated build directories were found inside the Git repository.' }

$result = foreach ($name in $publicNames) {
    $path = Join-Path $publicReleaseRoot $name
    [pscustomobject]@{
        Name = $name
        Length = (Get-Item -LiteralPath $path).Length
        SHA256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    }
}
$result | Format-Table -AutoSize
Write-Output "Public release: $publicReleaseRoot"
Write-Output "Private tester: $privateReleaseRoot"
Write-Output "Local handoff: $handoffRoot"

# Successful builds leave no staging tree behind.
Assert-ChildPath $buildRoot $localWorkRoot 'completed build staging'
Remove-Item -LiteralPath $buildRoot -Recurse -Force
if (Test-Path -LiteralPath $backupRoot -PathType Container) {
    Assert-ChildPath $backupRoot $localWorkRoot 'completed release backup'
    Remove-Item -LiteralPath $backupRoot -Recurse -Force
}
