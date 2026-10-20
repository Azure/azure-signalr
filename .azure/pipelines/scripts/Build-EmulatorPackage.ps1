[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $OutputDirectory,
    [Parameter(Mandatory)][ValidatePattern('^[1-9]\d*$')][string] $BuildId,
    [ValidateSet('preview', 'release')][string] $ReleaseType = 'preview',
    [string] $SignType = 'real',
    [ValidateSet('All', 'Build', 'Validate')][string] $Phase = 'All'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-DotNet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($args[0]) failed with exit code $LASTEXITCODE." }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$project = Join-Path $repoRoot 'src/Microsoft.Azure.SignalR.Emulator/Microsoft.Azure.SignalR.Emulator.csproj'
$tests = Join-Path $repoRoot 'test/Microsoft.Azure.SignalR.Emulator.Tests/Microsoft.Azure.SignalR.Emulator.Tests.csproj'
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ($Phase -ne 'Validate' -and (Test-Path $output) -and (Get-ChildItem $output -Force | Select-Object -First 1)) {
    throw "Package output directory must be empty: $output"
}
$isFinalBuild = $ReleaseType -eq 'release'
$versionSuffix = if ($isFinalBuild) { 'rtm' } else { "preview-$BuildId" }
$properties = "-p:BuildNumber=$BuildId;VersionSuffix=$versionSuffix;IsFinalBuild=$isFinalBuild;SignType=$SignType"
$version = & dotnet msbuild $project -getProperty:PackageVersion $properties
if ($LASTEXITCODE -ne 0) { throw "Unable to read emulator version: $version" }
$version = ($version -join "`n").Trim()
$versionPattern = if ($isFinalBuild) { '^\d+\.\d+\.\d+(?:\.\d+)?$' } else { "^\d+\.\d+\.\d+(?:\.\d+)?-preview-$BuildId`$" }
if ($version -cnotmatch $versionPattern) { throw "Invalid emulator $ReleaseType version: $version" }

if ($Phase -ne 'Validate') {
    Invoke-DotNet test $tests --configuration Release $properties --logger trx
}
# CI signs the selected package before validation.
$directory = Join-Path $output $ReleaseType
if ($Phase -ne 'Validate') {
    Invoke-DotNet pack $project --configuration Release --no-build --no-restore --output $directory `
        --include-symbols --include-source $properties
}
if ($Phase -ne 'Build') {
    & (Join-Path $PSScriptRoot 'Test-EmulatorPackage.ps1') -PackageDirectory $directory -ExpectedVersion $version
}
if ($Phase -eq 'Build') { return }

$packageFile = "Microsoft.Azure.SignalR.Emulator.$version.nupkg"
@{
    buildId = $BuildId
    sourceVersion = $env:BUILD_SOURCEVERSION
    version = $version
    packageFile = $packageFile
    packageSha256 = (Get-FileHash (Join-Path $directory $packageFile) -Algorithm SHA256).Hash.ToLowerInvariant()
    symbolsSha256 = (Get-FileHash (Join-Path $directory "Microsoft.Azure.SignalR.Emulator.$version.symbols.nupkg") -Algorithm SHA256).Hash.ToLowerInvariant()
} | ConvertTo-Json | Set-Content (Join-Path $output 'package-release.json') -Encoding utf8NoBOM
Copy-Item (Join-Path $repoRoot 'src/Microsoft.Azure.SignalR.Emulator/Dockerfile') $output
Copy-Item (Join-Path $PSScriptRoot 'Prepare-EmulatorRelease.ps1') $output
Copy-Item (Join-Path $PSScriptRoot 'Verify-EmulatorContainer.ps1') $output
