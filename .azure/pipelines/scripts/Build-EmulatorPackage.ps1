[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $OutputDirectory,
    [Parameter(Mandatory)][ValidatePattern('^[1-9]\d*$')][string] $BuildId,
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
$properties = "-p:BuildNumber=$BuildId;VersionSuffix=rtm;IsFinalBuild=true;SignType=$SignType"
$version = & dotnet msbuild $project -getProperty:PackageVersion $properties
if ($LASTEXITCODE -ne 0) { throw "Unable to read emulator version: $version" }
$version = ($version -join "`n").Trim()
if ($version -cnotmatch '^\d+\.\d+\.\d+(?:\.\d+)?$') { throw "Invalid emulator release version: $version" }

if ($Phase -ne 'Validate') {
    Invoke-DotNet test $tests --configuration Release $properties --logger trx
}
# Both packages contain the binaries built and tested above. CI signs them before validation.
foreach ($kind in @('release', 'preview')) {
    $packageVersion = if ($kind -eq 'release') { $version } else { "$version-preview-$BuildId" }
    $directory = Join-Path $output $kind
    if ($Phase -ne 'Validate') {
        Invoke-DotNet pack $project --configuration Release --no-build --no-restore --output $directory `
            --include-symbols --include-source $properties "-p:PackageVersion=$packageVersion"
    }
    if ($Phase -ne 'Build') {
        & (Join-Path $PSScriptRoot 'Test-EmulatorPackage.ps1') -PackageDirectory $directory -ExpectedVersion $packageVersion
    }
}
if ($Phase -eq 'Build') { return }

$packageFile = "Microsoft.Azure.SignalR.Emulator.$version.nupkg"
@{
    buildId = $BuildId
    sourceVersion = $env:BUILD_SOURCEVERSION
    version = $version
    packageFile = $packageFile
    packageSha256 = (Get-FileHash (Join-Path $output "release/$packageFile") -Algorithm SHA256).Hash.ToLowerInvariant()
    symbolsSha256 = (Get-FileHash (Join-Path $output "release/Microsoft.Azure.SignalR.Emulator.$version.symbols.nupkg") -Algorithm SHA256).Hash.ToLowerInvariant()
} | ConvertTo-Json | Set-Content (Join-Path $output 'package-release.json') -Encoding utf8NoBOM
Copy-Item (Join-Path $repoRoot 'src/Microsoft.Azure.SignalR.Emulator/Dockerfile') $output
Copy-Item (Join-Path $PSScriptRoot 'Prepare-EmulatorRelease.ps1') $output
