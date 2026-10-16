# Exercise release integrity checks without publishing packages or images.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$prepare = Join-Path $PSScriptRoot 'Prepare-EmulatorRelease.ps1'
$verify = Join-Path $PSScriptRoot 'Verify-EmulatorContainer.ps1'
$scratch = Join-Path ([IO.Path]::GetTempPath()) "signalr-release-test-$([guid]::NewGuid())"
$environmentNames = @('BUILD_BUILDID', 'BUILD_SOURCEVERSION', 'EMULATOR_CONTAINER_REGISTRY', 'EMULATOR_CONTAINER_REPOSITORY')
$originalEnvironment = @{}
foreach ($name in $environmentNames) { $originalEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }

function Assert-Rejected([scriptblock] $Action, [string] $Message) {
    try { & $Action }
    catch {
        if ($_.Exception.Message -like "*$Message*") { return }
        throw
    }
    throw "Expected rejection: $Message"
}

try {
    New-Item -ItemType Directory (Join-Path $scratch 'release') -Force | Out-Null
    $env:BUILD_BUILDID = '123'
    $env:BUILD_SOURCEVERSION = 'test-commit'
    $env:EMULATOR_CONTAINER_REGISTRY = 'example'
    $env:EMULATOR_CONTAINER_REPOSITORY = 'signalr/signalr-emulator'
    $package = Join-Path $scratch 'release/Microsoft.Azure.SignalR.Emulator.1.7.0.nupkg'
    $symbols = Join-Path $scratch 'release/Microsoft.Azure.SignalR.Emulator.1.7.0.symbols.nupkg'
    $archive = Join-Path $scratch 'signalr-emulator.1.7.0.linux-amd64.tar'
    Set-Content $package 'package'
    Set-Content $symbols 'symbols'
    Set-Content $archive 'image'
    $metadata = @{
        buildId = $env:BUILD_BUILDID
        sourceVersion = $env:BUILD_SOURCEVERSION
        version = '1.7.0'
        packageFile = [IO.Path]::GetFileName($package)
        packageSha256 = (Get-FileHash $package).Hash.ToLowerInvariant()
        symbolsSha256 = (Get-FileHash $symbols).Hash.ToLowerInvariant()
        archiveFile = [IO.Path]::GetFileName($archive)
        archiveSha256 = (Get-FileHash $archive).Hash.ToLowerInvariant()
        imageReference = 'signalr-emulator-build:123'
    }
    foreach ($file in @('package-release.json', 'container-release.json')) {
        $metadata | ConvertTo-Json | Set-Content (Join-Path $scratch $file)
    }
    foreach ($action in @('package', 'version', 'latest')) {
        & $prepare -ArtifactDirectory $scratch -Action $action
    }
    $env:BUILD_BUILDID = '124'
    Assert-Rejected { & $prepare -ArtifactDirectory $scratch -Action package } 'exact pipeline run'
    $env:BUILD_BUILDID = '123'
    $env:BUILD_SOURCEVERSION = 'another-commit'
    Assert-Rejected { & $prepare -ArtifactDirectory $scratch -Action version } 'exact pipeline run'
    $env:BUILD_SOURCEVERSION = 'test-commit'
    Add-Content $archive 'changed'
    Assert-Rejected { & $prepare -ArtifactDirectory $scratch -Action latest } 'does not match the bytes'
    Add-Content $package 'changed'
    Assert-Rejected { & $prepare -ArtifactDirectory $scratch -Action package } 'does not match the bytes'
    Set-Content $package 'package'
    Add-Content $symbols 'changed'
    Assert-Rejected { & $prepare -ArtifactDirectory $scratch -Action package } 'symbol package does not match'

    # ORAS descriptors are the contract used to compare the local and remote manifests.
    function Invoke-TestOras {
        $global:LASTEXITCODE = 0
        $digest = if ($args -contains '--oci-layout') { 'a' * 64 } else { 'b' * 64 }
        @{ digest = "sha256:$digest" } | ConvertTo-Json
    }
    Assert-Rejected { & $verify -OciLayout test -ImageReference test:123 -TargetImage example/test:123 -OrasPath Invoke-TestOras } 'does not match the local image digest'
    Write-Host 'Release integrity checks passed.'
}
finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $originalEnvironment[$name]) }
    $resolved = [IO.Path]::GetFullPath($scratch)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if (-not $resolved.StartsWith($tempRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a directory outside the temporary directory: $resolved"
    }
    if (Test-Path $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
