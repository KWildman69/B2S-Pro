[CmdletBinding()]
param(
    [switch]$IncludeLocalPackage,
    [string]$OutputRoot,
    [string]$AssetRoot
)

$ErrorActionPreference = 'Stop'
$installerRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = Split-Path -Parent $installerRoot
$workspaceRoot = Split-Path -Parent $repositoryRoot
$localWorkRoot = Join-Path $workspaceRoot 'B2S-Local-Work'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $localWorkRoot 'Installer-Dist'
}
if ([string]::IsNullOrWhiteSpace($AssetRoot)) {
    $AssetRoot = Join-Path $localWorkRoot 'Current-Release\Public'
}
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source = Join-Path $installerRoot 'B2SProInstaller.cs'
$manifest = Join-Path $installerRoot 'B2SProInstaller.manifest'
$updateCheckerSource = Join-Path $installerRoot 'B2SUpdateChecker.cs'
$updateCheckerManifest = Join-Path $installerRoot 'B2SUpdateChecker.manifest'
$icon = Join-Path $repositoryRoot 'src\designer\b2sbackglassdesigner\B2SPro.ico'
$logo = Join-Path $repositoryRoot 'src\designer\b2sbackglassdesigner\Resources\B2SProHeader.png'
$proOutput = Join-Path $OutputRoot 'B2SProSetup.exe'
$serverOutput = Join-Path $OutputRoot 'B2SServerSetup.exe'
$updateCheckerOutput = Join-Path $OutputRoot 'B2SUpdateChecker.exe'
$testOutput = Join-Path $OutputRoot 'B2SSetup.SelfTest.exe'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "The .NET Framework C# compiler was not found at $compiler"
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$commonCompilerArguments = @(
    '/nologo'
    '/target:winexe'
    '/platform:anycpu'
    '/optimize+'
    '/debug-'
    "/win32manifest:$manifest"
    "/win32icon:$icon"
    "/resource:$logo,B2SProHeader.png"
    '/reference:System.dll'
    '/reference:System.Core.dll'
    '/reference:System.Drawing.dll'
    '/reference:System.Windows.Forms.dll'
    '/reference:System.Net.Http.dll'
    '/reference:System.IO.Compression.dll'
    '/reference:System.IO.Compression.FileSystem.dll'
    '/reference:System.Web.Extensions.dll'
)

$proCompilerArguments = @($commonCompilerArguments) + @(
    "/out:$proOutput"
    $source
)

& $compiler $proCompilerArguments

if ($LASTEXITCODE -ne 0) {
    throw "B2S Pro installer compilation failed with exit code $LASTEXITCODE"
}

$serverCompilerArguments = @($commonCompilerArguments) + @(
    '/define:SERVER_ONLY'
    "/out:$serverOutput"
    $source
)

& $compiler $serverCompilerArguments
if ($LASTEXITCODE -ne 0) {
    throw "B2S Server installer compilation failed with exit code $LASTEXITCODE"
}

$updateCheckerArguments = @(
    '/nologo'
    '/target:winexe'
    '/platform:anycpu'
    '/optimize+'
    '/debug-'
    "/win32manifest:$updateCheckerManifest"
    "/win32icon:$icon"
    '/reference:System.dll'
    '/reference:System.Core.dll'
    '/reference:System.Windows.Forms.dll'
    '/reference:System.Net.Http.dll'
    '/reference:System.Web.Extensions.dll'
    "/out:$updateCheckerOutput"
    $updateCheckerSource
)

& $compiler $updateCheckerArguments
if ($LASTEXITCODE -ne 0) {
    throw "B2S update checker compilation failed with exit code $LASTEXITCODE"
}
& $updateCheckerOutput --self-test
if ($LASTEXITCODE -ne 0) {
    throw "B2S update checker self-test failed with exit code $LASTEXITCODE"
}

foreach ($builtPath in @($proOutput, $serverOutput, $updateCheckerOutput)) {
    $built = Get-Item -LiteralPath $builtPath
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $builtPath
    Write-Output "Built: $($built.FullName)"
    Write-Output "Size:  $($built.Length) bytes"
    Write-Output "SHA256: $($hash.Hash)"
}

$testArguments = @(
    '/nologo'
    '/target:exe'
    '/platform:anycpu'
    '/optimize+'
    '/debug-'
    '/nowin32manifest'
    "/win32icon:$icon"
    "/resource:$logo,B2SProHeader.png"
    '/reference:System.dll'
    '/reference:System.Core.dll'
    '/reference:System.Drawing.dll'
    '/reference:System.Windows.Forms.dll'
    '/reference:System.Net.Http.dll'
    '/reference:System.IO.Compression.dll'
    '/reference:System.IO.Compression.FileSystem.dll'
    '/reference:System.Web.Extensions.dll'
    "/out:$testOutput"
    $source
)

& $compiler $testArguments
if ($LASTEXITCODE -ne 0) {
    throw "Self-test runner compilation failed with exit code $LASTEXITCODE"
}
Write-Output "Test runner: $testOutput"

if ($IncludeLocalPackage) {
    $packages = @(
        (Join-Path $AssetRoot 'B2S-Pro-Backglass-1.0.3.zip'),
        (Join-Path $AssetRoot 'B2S-Pro-Server-3.0.2.zip')
    )
    foreach ($package in $packages) {
        $sidecar = "$package.sha256"
        if (-not (Test-Path -LiteralPath $package) -or -not (Test-Path -LiteralPath $sidecar)) {
            throw "The local release package or SHA-256 sidecar is missing: $package"
        }
        Copy-Item -LiteralPath $package -Destination $OutputRoot -Force
        Copy-Item -LiteralPath $sidecar -Destination $OutputRoot -Force
    }
    Write-Output 'Included the verified Designer and Server packages for private/offline testing.'
}
