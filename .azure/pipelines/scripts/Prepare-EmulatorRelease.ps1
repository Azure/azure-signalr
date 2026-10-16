[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $ArtifactDirectory,
    [Parameter(Mandatory)][ValidateSet('package', 'version', 'latest')][string] $Action
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path $ArtifactDirectory).Path
$metadataFile = if ($Action -eq 'package') { 'package-release.json' } else { 'container-release.json' }
$metadata = Get-Content (Join-Path $root $metadataFile) -Raw | ConvertFrom-Json
if ($metadata.buildId -cne $env:BUILD_BUILDID -or $metadata.sourceVersion -cne $env:BUILD_SOURCEVERSION) {
    throw 'The release artifact must come from this exact pipeline run and source commit.'
}
$version = [string] $metadata.version
if ($version -cnotmatch '^\d+\.\d+\.\d+(?:\.\d+)?$') { throw 'Invalid emulator release version.' }
if ($Action -eq 'package') {
    if ($metadata.packageFile -cne "Microsoft.Azure.SignalR.Emulator.$version.nupkg") { throw 'Invalid package filename.' }
    $path = Join-Path $root "release/$($metadata.packageFile)"
    $expectedHash = $metadata.packageSha256
}
else {
    if ($metadata.archiveFile -cne "signalr-emulator.$version.linux-amd64.tar") { throw 'Invalid image archive filename.' }
    $path = Join-Path $root $metadata.archiveFile
    $expectedHash = $metadata.archiveSha256
}
if ((Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $expectedHash) {
    throw 'The release artifact does not match the bytes validated by the build stage.'
}
Write-Host "##vso[task.setvariable variable=emulatorReleaseVersion]$version"
if ($Action -eq 'package') {
    $symbols = Join-Path $root "release/Microsoft.Azure.SignalR.Emulator.$version.symbols.nupkg"
    if ((Get-FileHash $symbols -Algorithm SHA256).Hash.ToLowerInvariant() -cne $metadata.symbolsSha256) {
        throw 'The symbol package does not match the bytes validated by the build stage.'
    }
    Write-Host "##vso[task.setvariable variable=emulatorPackagePath]$root/release/*.nupkg"
    return
}

$registryName = $env:EMULATOR_CONTAINER_REGISTRY
if ($registryName -cnotmatch '^[a-zA-Z0-9]+$') { throw 'Set EmulatorContainerRegistry to the ACR name.' }
$repository = $env:EMULATOR_CONTAINER_REPOSITORY
if ($repository -cnotmatch '^[a-z0-9]+(?:[._/-][a-z0-9]+)*$') { throw 'Invalid emulator container repository.' }
$registry = $registryName.ToLowerInvariant() + '.azurecr.io'
$tag = if ($Action -eq 'latest') { 'latest' } else { $version }
Write-Host "##vso[task.setvariable variable=emulatorRegistry]$registry"
Write-Host "##vso[task.setvariable variable=emulatorImageReference]$($metadata.imageReference)"
Write-Host "##vso[task.setvariable variable=emulatorArchivePath]$path"
Write-Host "##vso[task.setvariable variable=emulatorTargetImage]${registry}/${repository}:$tag"
