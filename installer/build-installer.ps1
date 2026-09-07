[CmdletBinding()]
param(
    [switch]$IncludeLocalPackage
)

$ErrorActionPreference = 'Stop'
$installerRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = Split-Path -Parent $installerRoot
$outputRoot = Join-Path $installerRoot 'dist'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source = Join-Path $installerRoot 'B2SProInstaller.cs'
$manifest = Join-Path $installerRoot 'B2SProInstaller.manifest'
$icon = Join-Path $repositoryRoot 'src\designer\b2sbackglassdesigner\B2SPro.ico'
$logo = Join-Path $repositoryRoot 'src\designer\b2sbackglassdesigner\Resources\B2SProHeader.png'
$proOutput = Join-Path $outputRoot 'B2SProSetup.exe'
$serverOutput = Join-Path $outputRoot 'B2SServerSetup.exe'
$testOutput = Join-Path $outputRoot 'B2SSetup.SelfTest.exe'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "The .NET Framework C# compiler was not found at $compiler"
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

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

foreach ($builtPath in @($proOutput, $serverOutput)) {
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
    $assetRoot = Join-Path $repositoryRoot 'release-assets'
    $packages = @(
        (Join-Path $assetRoot 'B2S-Latest-Complete-Build.zip'),
        (Join-Path $assetRoot 'B2S-Pro-Server-3.0.0.zip')
    )
    foreach ($package in $packages) {
        $sidecar = "$package.sha256"
        if (-not (Test-Path -LiteralPath $package) -or -not (Test-Path -LiteralPath $sidecar)) {
            throw "The local release package or SHA-256 sidecar is missing: $package"
        }
        Copy-Item -LiteralPath $package -Destination $outputRoot -Force
        Copy-Item -LiteralPath $sidecar -Destination $outputRoot -Force
    }
    Write-Output 'Included the verified complete and server-only packages for private/offline testing.'
}
