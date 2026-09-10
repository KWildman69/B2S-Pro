[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Core') {
    throw 'Build-CurrentRelease.ps1 must be run with PowerShell 7 (pwsh) so ZIP entry paths use installer-compatible forward slashes.'
}
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$workspaceRoot = (Resolve-Path -LiteralPath (Join-Path $repositoryRoot '..')).Path
$designerRoot = Join-Path $repositoryRoot 'src\designer'
$serverRoot = Join-Path $repositoryRoot 'src\server'
$releaseRoot = Join-Path $repositoryRoot 'release-assets'
$installerRoot = Join-Path $repositoryRoot 'installer'
$distRoot = Join-Path $installerRoot 'dist'
$outerDesignerRoot = Join-Path $workspaceRoot 'B2S-Pro-Backglass-1.0.1'
$outerServerRoot = Join-Path $workspaceRoot 'B2S-Pro-Server-3.0.0'
$outerHelpRoot = Join-Path $workspaceRoot 'B2SProHelp-Source'
$offlineProRoot = Join-Path $workspaceRoot 'B2S-Pro-Offline-Setup-1.0.1'
$offlineServerRoot = Join-Path $workspaceRoot 'B2S-Server-Offline-Setup-3.0.0'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupRoot = Join-Path (Join-Path $workspaceRoot 'Package-Backups') ($stamp + '-before-final-verified-build')
$localToolsRoot = Join-Path $repositoryRoot '.tools'
$buildRoot = Join-Path $localToolsRoot ('release-' + $stamp)

foreach ($path in @($repositoryRoot, $workspaceRoot, $designerRoot, $serverRoot, $releaseRoot, $installerRoot)) {
    if (-not (Test-Path -LiteralPath $path -PathType Container)) {
        throw "Required release path is missing: $path"
    }
}
New-Item -ItemType Directory -Path $localToolsRoot -Force | Out-Null
if (-not $buildRoot.StartsWith((Resolve-Path -LiteralPath $localToolsRoot).Path + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe temporary build path: $buildRoot"
}
if (-not $outerServerRoot.StartsWith($workspaceRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe server handoff path: $outerServerRoot"
}

New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null

function Backup-File([string]$path, [string]$relativeName) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return }
    $destination = Join-Path $backupRoot $relativeName
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $path -Destination $destination -Force
}

function Backup-Directory([string]$path, [string]$relativeName) {
    if (-not (Test-Path -LiteralPath $path -PathType Container)) { return }
    $destination = Join-Path $backupRoot $relativeName
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-DirectoryContents $path $destination
}

function Copy-DirectoryContents([string]$source, [string]$destination) {
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Get-ChildItem -LiteralPath $source -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $destination -Recurse -Force
    }
}

function New-Zip([string]$sourceDirectory, [string]$destinationZip) {
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

$workspaceFilesToReplace = @(
    'B2SPro.exe',
    'B2SProSetup.exe',
    'B2SServerSetup.exe',
    'B2S-Latest-Complete-Build.zip',
    'B2S-Latest-Complete-Build.zip.sha256',
    'B2S-Pro-Backglass-1.0.1.zip',
    'B2S-Pro-Backglass-Source-1.0.1.zip',
    'B2S-Pro-Server-3.0.0.zip',
    'B2S-Pro-Server-Source-3.0.0.zip',
    'B2SBackglassServer.dll',
    'B2SBackglassServerEXE.exe',
    'B2S-Pro-Offline-Setup-1.0.1.zip',
    'B2S-Pro-Private-Tester-Package-1.0.1.zip',
    'B2S-Server-Offline-Setup-3.0.0.zip'
)
foreach ($name in $workspaceFilesToReplace) {
    Backup-File (Join-Path $workspaceRoot $name) (Join-Path 'workspace' $name)
}

$releaseFilesToReplace = @(
    'B2SProSetup.exe',
    'B2SServerSetup.exe',
    'B2S-Latest-Complete-Build.zip',
    'B2S-Latest-Complete-Build.zip.sha256',
    'B2S-Pro-Backglass-1.0.1.zip',
    'B2S-Pro-Backglass-Source-1.0.1.zip',
    'B2S-Pro-Server-3.0.0.zip',
    'B2S-Pro-Server-3.0.0.zip.sha256',
    'B2S-Pro-Server-Source-3.0.0.zip',
    'SHA256SUMS.txt'
)
foreach ($name in $releaseFilesToReplace) {
    Backup-File (Join-Path $releaseRoot $name) (Join-Path 'release-assets' $name)
}

foreach ($name in @('B2SProSetup.exe', 'B2SServerSetup.exe', 'B2S-Latest-Complete-Build.zip', 'B2S-Latest-Complete-Build.zip.sha256', 'B2S-Pro-Server-3.0.0.zip', 'B2S-Pro-Server-3.0.0.zip.sha256')) {
    Backup-File (Join-Path $distRoot $name) (Join-Path 'installer-dist' $name)
}
foreach ($entry in @(
    @{ Root = $offlineProRoot; Names = @('B2SProSetup.exe', 'B2S-Latest-Complete-Build.zip', 'B2S-Latest-Complete-Build.zip.sha256'); Prefix = 'offline-pro' },
    @{ Root = $offlineServerRoot; Names = @('B2SServerSetup.exe', 'B2S-Pro-Server-3.0.0.zip', 'B2S-Pro-Server-3.0.0.zip.sha256'); Prefix = 'offline-server' }
)) {
    foreach ($name in $entry.Names) {
        Backup-File (Join-Path $entry.Root $name) (Join-Path $entry.Prefix $name)
    }
}

Backup-Directory $outerDesignerRoot (Join-Path 'workspace' 'B2S-Pro-Backglass-1.0.1')
Backup-Directory $outerServerRoot (Join-Path 'workspace' 'B2S-Pro-Server-3.0.0')
Backup-Directory $outerHelpRoot (Join-Path 'workspace' 'B2SProHelp-Source')

# Compile the embedded help before either Designer architecture so both EXEs
# contain the current guide.
$helpSourceRoot = Join-Path $repositoryRoot 'docs\help-source'
$helpCompiler = Join-Path $repositoryRoot '.tools\htmlhelp2\hhc.exe'
$compiledHelp = Join-Path $helpSourceRoot 'B2SProHelp.chm'
if (-not (Test-Path -LiteralPath $helpCompiler -PathType Leaf)) { throw "HTML Help compiler is missing: $helpCompiler" }
Push-Location $helpSourceRoot
try {
    & $helpCompiler 'B2SBackglassDesigner.hhp' | Out-Host
}
finally {
    Pop-Location
}
if (-not (Test-Path -LiteralPath $compiledHelp -PathType Leaf)) { throw 'B2S Pro help compilation did not produce B2SProHelp.chm.' }
$helpErrorLog = Join-Path $helpSourceRoot '_errorlog.txt'
if (Test-Path -LiteralPath $helpErrorLog -PathType Leaf) { Remove-Item -LiteralPath $helpErrorLog -Force }
Copy-Item -LiteralPath $compiledHelp -Destination (Join-Path $designerRoot 'b2sbackglassdesigner\Resources\B2SProHelp.chm') -Force
Copy-DirectoryContents $helpSourceRoot $outerHelpRoot

# Build every distributed executable from the same verified source state.
$msBuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path -LiteralPath $msBuild -PathType Leaf)) { throw "MSBuild is missing: $msBuild" }
& $msBuild (Join-Path $designerRoot 'B2SBackglassDesigner.sln') /t:Rebuild /p:Configuration=Release /p:Platform=x64 /m:1 /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'x64 Designer build failed.' }
& $msBuild (Join-Path $designerRoot 'B2SBackglassDesigner.sln') /t:Rebuild /p:Configuration=Release /p:Platform=x86 /m:1 /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'x86 Designer build failed.' }
& $msBuild (Join-Path $serverRoot 'b2sbackglassserver\B2SBackglassServer.sln') /t:Rebuild /p:Configuration=Release /p:Platform='Any CPU' /m:1 /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'B2S Pro Server build failed.' }

# Keep the visible source/runtime folder current for local handoff.
if (Test-Path -LiteralPath $outerDesignerRoot) { Remove-Item -LiteralPath $outerDesignerRoot -Recurse -Force }
Copy-DirectoryContents $designerRoot $outerDesignerRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'CHANGELOG.md') -Destination (Join-Path $outerDesignerRoot 'B2S-Pro-Changelog.md') -Force
$outerDesignerTests = Join-Path $outerDesignerRoot 'tests'
if (Test-Path -LiteralPath $outerDesignerTests) { Remove-Item -LiteralPath $outerDesignerTests -Recurse -Force }

$x64Designer = Join-Path $designerRoot 'b2sbackglassdesigner\bin\x64\Release\B2SPro.exe'
$x86Designer = Join-Path $designerRoot 'b2sbackglassdesigner\bin\x86\Release\B2SPro.exe'
foreach ($path in @($x64Designer, $x86Designer)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Designer build is missing: $path" }
}
Copy-Item -LiteralPath $x64Designer -Destination (Join-Path $workspaceRoot 'B2SPro.exe') -Force

# Build the two Designer archives from the verified source tree.
$designerSourceStage = Join-Path $buildRoot 'designer-source'
Copy-DirectoryContents $designerRoot $designerSourceStage
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'CHANGELOG.md') -Destination (Join-Path $designerSourceStage 'B2S-Pro-Changelog.md') -Force
Get-ChildItem -LiteralPath $designerSourceStage -Recurse -Directory -Force |
    Where-Object { $_.Name -in @('bin', 'obj', '.vs', 'tests') } |
    Sort-Object FullName -Descending |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName -Recurse -Force }

$designerRuntimeStage = Join-Path $buildRoot 'designer-runtime'
Copy-DirectoryContents $designerSourceStage $designerRuntimeStage
foreach ($name in @('.gitattributes', '.gitignore', 'tests')) {
    $path = Join-Path $designerRuntimeStage $name
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
}

$designerZip = Join-Path $buildRoot 'B2S-Pro-Backglass-1.0.1.zip'
$designerSourceZip = Join-Path $buildRoot 'B2S-Pro-Backglass-Source-1.0.1.zip'
New-Zip $designerRuntimeStage $designerZip
New-Zip $designerSourceStage $designerSourceZip

# Rebuild the server-only runtime and source archives. The established server
# package supplies the official utilities and templates; only the newly built
# server binaries and current documentation are replaced.
$oldServerZip = Join-Path $releaseRoot 'B2S-Pro-Server-3.0.0.zip'
$serverRuntimeStage = Join-Path $buildRoot 'server-runtime'
[System.IO.Compression.ZipFile]::ExtractToDirectory($oldServerZip, $serverRuntimeStage)
$serverDll = Join-Path $serverRoot 'b2sbackglassserver\b2sbackglassserver\bin\Release\B2SBackglassServer.dll'
$serverExe = Join-Path $serverRoot 'b2sbackglassserver\B2SBackglassServer\bin\Release\B2SBackglassServerEXE.exe'
foreach ($path in @($serverDll, $serverExe)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Server build is missing: $path" }
}
Copy-Item -LiteralPath $serverDll -Destination (Join-Path $serverRuntimeStage 'B2SBackglassServer.dll') -Force
Copy-Item -LiteralPath $serverExe -Destination (Join-Path $serverRuntimeStage 'B2SBackglassServerEXE.exe') -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'CHANGELOG.md') -Destination (Join-Path $serverRuntimeStage 'B2S-Pro-Changelog.md') -Force

$serverSourceStage = Join-Path $buildRoot 'server-source'
Copy-DirectoryContents $serverRoot $serverSourceStage
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'CHANGELOG.md') -Destination (Join-Path $serverSourceStage 'B2S-Pro-Changelog.md') -Force
Get-ChildItem -LiteralPath $serverSourceStage -Recurse -Directory -Force |
    Where-Object { $_.Name -in @('bin', 'obj', '.vs', 'tests') } |
    Sort-Object FullName -Descending |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName -Recurse -Force }

$serverZip = Join-Path $buildRoot 'B2S-Pro-Server-3.0.0.zip'
$serverSidecar = $serverZip + '.sha256'
$serverSourceZip = Join-Path $buildRoot 'B2S-Pro-Server-Source-3.0.0.zip'
New-Zip $serverRuntimeStage $serverZip
Write-ShaSidecar $serverZip $serverSidecar
New-Zip $serverSourceStage $serverSourceZip

# Preserve the established complete-package layout and replace only the current
# Designer and Server runtime, source, and release notes.
$oldCompleteZip = Join-Path $releaseRoot 'B2S-Latest-Complete-Build.zip'
$completeStage = Join-Path $buildRoot 'complete'
[System.IO.Compression.ZipFile]::ExtractToDirectory($oldCompleteZip, $completeStage)
$completeDesignerSource = Join-Path $completeStage 'Source\B2S Pro'
$completeServerSource = Join-Path $completeStage 'Source\B2S Server'
Remove-Item -LiteralPath $completeDesignerSource -Recurse -Force
Remove-Item -LiteralPath $completeServerSource -Recurse -Force
Copy-DirectoryContents $designerSourceStage $completeDesignerSource
Copy-DirectoryContents $serverSourceStage $completeServerSource
Copy-Item -LiteralPath $x64Designer -Destination (Join-Path $completeStage 'Runtime\x64\B2SPro.exe') -Force
Copy-Item -LiteralPath $x86Designer -Destination (Join-Path $completeStage 'Runtime\x86\B2SPro.exe') -Force
Copy-Item -LiteralPath (Join-Path $designerRoot 'b2sbackglassdesigner\bin\x64\Release\B2SPro.exe.config') -Destination (Join-Path $completeStage 'Runtime\x64\B2SPro.exe.config') -Force
Copy-Item -LiteralPath (Join-Path $designerRoot 'b2sbackglassdesigner\bin\x86\Release\B2SPro.exe.config') -Destination (Join-Path $completeStage 'Runtime\x86\B2SPro.exe.config') -Force
Copy-Item -LiteralPath (Join-Path $designerRoot 'B2SVPinMAMEStarter\bin\x64\Release\B2SVPinMAMEStarter.exe') -Destination (Join-Path $completeStage 'Runtime\x64\B2SVPinMAMEStarter.exe') -Force
Copy-Item -LiteralPath (Join-Path $designerRoot 'B2SVPinMAMEStarter\bin\x86\Release\B2SVPinMAMEStarter.exe') -Destination (Join-Path $completeStage 'Runtime\x86\B2SVPinMAMEStarter.exe') -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'CHANGELOG.md') -Destination (Join-Path $completeStage 'Documentation\B2S-Pro-Changelog.md') -Force
Copy-DirectoryContents $serverRuntimeStage (Join-Path $completeStage 'Runtime\B2SServer')

$completeZip = Join-Path $buildRoot 'B2S-Latest-Complete-Build.zip'
$completeSidecar = $completeZip + '.sha256'
New-Zip $completeStage $completeZip
Write-ShaSidecar $completeZip $completeSidecar

# Publish the verified archives into local release staging before compiling the
# installers so -IncludeLocalPackage copies these exact files.
Copy-Item -LiteralPath $designerZip -Destination (Join-Path $releaseRoot 'B2S-Pro-Backglass-1.0.1.zip') -Force
Copy-Item -LiteralPath $designerSourceZip -Destination (Join-Path $releaseRoot 'B2S-Pro-Backglass-Source-1.0.1.zip') -Force
Copy-Item -LiteralPath $completeZip -Destination (Join-Path $releaseRoot 'B2S-Latest-Complete-Build.zip') -Force
Copy-Item -LiteralPath $completeSidecar -Destination (Join-Path $releaseRoot 'B2S-Latest-Complete-Build.zip.sha256') -Force
Copy-Item -LiteralPath $serverZip -Destination (Join-Path $releaseRoot 'B2S-Pro-Server-3.0.0.zip') -Force
Copy-Item -LiteralPath $serverSidecar -Destination (Join-Path $releaseRoot 'B2S-Pro-Server-3.0.0.zip.sha256') -Force
Copy-Item -LiteralPath $serverSourceZip -Destination (Join-Path $releaseRoot 'B2S-Pro-Server-Source-3.0.0.zip') -Force

& (Join-Path $installerRoot 'build-installer.ps1') -IncludeLocalPackage

$selfTest = Join-Path $distRoot 'B2SSetup.SelfTest.exe'
& $selfTest --self-test (Join-Path $distRoot 'B2S-Latest-Complete-Build.zip') (Join-Path $distRoot 'B2S-Pro-Server-3.0.0.zip')
if ($LASTEXITCODE -ne 0) { throw "Installer self-test failed with exit code $LASTEXITCODE" }

Copy-Item -LiteralPath (Join-Path $distRoot 'B2SProSetup.exe') -Destination (Join-Path $releaseRoot 'B2SProSetup.exe') -Force
Copy-Item -LiteralPath (Join-Path $distRoot 'B2SServerSetup.exe') -Destination (Join-Path $releaseRoot 'B2SServerSetup.exe') -Force

# Refresh the root handoff files and both three-file offline folders.
foreach ($name in @('B2S-Latest-Complete-Build.zip', 'B2S-Latest-Complete-Build.zip.sha256', 'B2S-Pro-Backglass-1.0.1.zip', 'B2S-Pro-Backglass-Source-1.0.1.zip', 'B2S-Pro-Server-3.0.0.zip', 'B2S-Pro-Server-Source-3.0.0.zip', 'B2SProSetup.exe', 'B2SServerSetup.exe')) {
    Copy-Item -LiteralPath (Join-Path $releaseRoot $name) -Destination (Join-Path $workspaceRoot $name) -Force
}
Copy-Item -LiteralPath $serverDll -Destination (Join-Path $workspaceRoot 'B2SBackglassServer.dll') -Force
Copy-Item -LiteralPath $serverExe -Destination (Join-Path $workspaceRoot 'B2SBackglassServerEXE.exe') -Force
if (Test-Path -LiteralPath $outerServerRoot) { Remove-Item -LiteralPath $outerServerRoot -Recurse -Force }
Copy-DirectoryContents $serverRuntimeStage $outerServerRoot
New-Item -ItemType Directory -Path $offlineProRoot -Force | Out-Null
foreach ($name in @('B2SProSetup.exe', 'B2S-Latest-Complete-Build.zip', 'B2S-Latest-Complete-Build.zip.sha256')) {
    Copy-Item -LiteralPath (Join-Path $releaseRoot $name) -Destination (Join-Path $offlineProRoot $name) -Force
}
New-Item -ItemType Directory -Path $offlineServerRoot -Force | Out-Null
foreach ($name in @('B2SServerSetup.exe', 'B2S-Pro-Server-3.0.0.zip', 'B2S-Pro-Server-3.0.0.zip.sha256')) {
    Copy-Item -LiteralPath (Join-Path $releaseRoot $name) -Destination (Join-Path $offlineServerRoot $name) -Force
}

New-Zip $offlineProRoot (Join-Path $workspaceRoot 'B2S-Pro-Offline-Setup-1.0.1.zip')
New-Zip $offlineProRoot (Join-Path $workspaceRoot 'B2S-Pro-Private-Tester-Package-1.0.1.zip')
New-Zip $offlineServerRoot (Join-Path $workspaceRoot 'B2S-Server-Offline-Setup-3.0.0.zip')

$sumNames = @(
    'B2SProSetup.exe',
    'B2SServerSetup.exe',
    'B2S-Latest-Complete-Build.zip',
    'B2S-Latest-Complete-Build.zip.sha256',
    'B2S-Pro-Backglass-1.0.1.zip',
    'B2S-Pro-Backglass-Source-1.0.1.zip',
    'B2S-Pro-Server-3.0.0.zip',
    'B2S-Pro-Server-3.0.0.zip.sha256',
    'B2S-Pro-Server-Source-3.0.0.zip'
)
$sumLines = foreach ($name in $sumNames) {
    $path = Join-Path $releaseRoot $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Release checksum target is missing: $path" }
    (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash + '  ' + $name
}
Set-Content -LiteralPath (Join-Path $releaseRoot 'SHA256SUMS.txt') -Value $sumLines -Encoding ascii

$result = foreach ($name in @(
    'B2SProSetup.exe',
    'B2SServerSetup.exe',
    'B2S-Latest-Complete-Build.zip',
    'B2S-Latest-Complete-Build.zip.sha256',
    'B2S-Pro-Backglass-1.0.1.zip',
    'B2S-Pro-Backglass-Source-1.0.1.zip'
    'B2S-Pro-Server-3.0.0.zip'
    'B2S-Pro-Server-3.0.0.zip.sha256'
    'B2S-Pro-Server-Source-3.0.0.zip'
)) {
    $path = Join-Path $releaseRoot $name
    [pscustomobject]@{
        Name = $name
        Length = (Get-Item -LiteralPath $path).Length
        SHA256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    }
}
$result | Format-Table -AutoSize
Write-Output "Backup: $backupRoot"
Write-Output "Build staging: $buildRoot"
