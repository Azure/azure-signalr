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

    # OneBranch exports a Docker archive. Only accept the tested package's image.
    $complete = Join-Path $PSScriptRoot 'Complete-EmulatorContainer.ps1'
    $imageDirectory = Join-Path $scratch 'saved-image'
    New-Item -ItemType Directory $imageDirectory | Out-Null
    $containerOutput = Join-Path $scratch 'container-artifact'
    New-Item -ItemType Directory $containerOutput | Out-Null
    $archive = Join-Path $containerOutput $metadata.archiveFile
    foreach ($helper in @('Prepare-EmulatorRelease.ps1', 'Verify-EmulatorContainer.ps1')) {
        Copy-Item (Join-Path $PSScriptRoot $helper) $scratch
        Add-Content (Join-Path $scratch $helper) '# Artifact copy used by the release test.'
    }
    $configuration = @{
        os = 'linux'
        architecture = 'amd64'
        config = @{ Labels = @{
            'org.opencontainers.image.version' = $metadata.version
            'org.opencontainers.image.revision' = $metadata.sourceVersion
            'com.azure.signalr.package.sha256' = $metadata.packageSha256
        } }
    }
    # Match OneBranch archives that contain both case-sensitive JSON keys.
    $configuration = $configuration | ConvertTo-Json -Depth 4 | ConvertFrom-Json -AsHashtable
    $configuration.config['labels'] = @{ BaseImage = 'onebranch-test-image:123' }
    $packageMetadata = @{}
    foreach ($key in @('buildId', 'sourceVersion', 'version', 'packageFile', 'packageSha256', 'symbolsSha256')) {
        $packageMetadata[$key] = $metadata[$key]
    }
    $packageMetadata | ConvertTo-Json | Set-Content (Join-Path $scratch 'package-release.json')
    function Save-TestImage {
        $configPath = Join-Path $imageDirectory 'config.json'
        $configuration | ConvertTo-Json -Depth 4 | Set-Content $configPath
        $configFile = (Get-FileHash $configPath).Hash.ToLowerInvariant() + '.json'
        Copy-Item $configPath (Join-Path $imageDirectory $configFile)
        ConvertTo-Json -InputObject @(@{ Config = $configFile; RepoTags = @('onebranch.azurecr.io/signalr-emulator-build:123') }) |
            Set-Content (Join-Path $imageDirectory 'manifest.json')
        & tar -cf $archive -C $imageDirectory manifest.json $configFile
        if ($LASTEXITCODE -ne 0) { throw 'Could not create test image archive.' }
    }
    Save-TestImage
    & $complete -PackageDirectory $scratch -OutputDirectory $containerOutput
    & $prepare -ArtifactDirectory $containerOutput -Action version
    foreach ($helper in @('Prepare-EmulatorRelease.ps1', 'Verify-EmulatorContainer.ps1')) {
        if ((Get-FileHash (Join-Path $scratch $helper)).Hash -cne (Get-FileHash (Join-Path $containerOutput $helper)).Hash) {
            throw 'Container release helpers must preserve the package artifact bytes.'
        }
    }
    $configuration.config.Labels.'com.azure.signalr.package.sha256' = 'wrong-package'
    Save-TestImage
    Assert-Rejected { & $complete -PackageDirectory $scratch -OutputDirectory $containerOutput } 'source and package must match'
    $configuration.config.Labels.'com.azure.signalr.package.sha256' = $metadata.packageSha256
    $configuration.config.Labels.'org.opencontainers.image.revision' = 'wrong-commit'
    Save-TestImage
    Assert-Rejected { & $complete -PackageDirectory $scratch -OutputDirectory $containerOutput } 'source and package must match'

    # Preview images remain test artifacts and cannot enter a production release.
    $previewVersion = "$($metadata.version)-preview-$($metadata.buildId)"
    $packageMetadata.version = $previewVersion
    $packageMetadata.packageFile = "Microsoft.Azure.SignalR.Emulator.$previewVersion.nupkg"
    $packageMetadata | ConvertTo-Json | Set-Content (Join-Path $scratch 'package-release.json')
    $configuration.config.Labels.'org.opencontainers.image.version' = $previewVersion
    $configuration.config.Labels.'org.opencontainers.image.revision' = $metadata.sourceVersion
    $archive = Join-Path $containerOutput "signalr-emulator.$previewVersion.linux-amd64.tar"
    Save-TestImage
    & $complete -PackageDirectory $scratch -OutputDirectory $containerOutput
    foreach ($previewAction in @('version', 'latest')) {
        Assert-Rejected { & $prepare -ArtifactDirectory $containerOutput -Action $previewAction } 'Invalid emulator release version'
    }
    Assert-Rejected { & $prepare -ArtifactDirectory $scratch -Action package } 'Invalid emulator release version'
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
