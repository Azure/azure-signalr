[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $PackageDirectory,
    [Parameter(Mandatory)][string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$metadata = Get-Content (Join-Path $PackageDirectory 'package-release.json') -Raw | ConvertFrom-Json
if ($metadata.buildId -cne $env:BUILD_BUILDID -or $metadata.sourceVersion -cne $env:BUILD_SOURCEVERSION) {
    throw 'Container inputs must come from this exact pipeline run and source commit.'
}
if ($metadata.version -cnotmatch '^\d+\.\d+\.\d+(?:\.\d+)?$') { throw 'Invalid emulator release version.' }
$archiveFile = "signalr-emulator.$($metadata.version).linux-amd64.tar"
$archivePath = Join-Path $OutputDirectory $archiveFile
function Read-ImageJson([string] $Path) {
    $json = & tar -xOf $archivePath $Path
    if ($LASTEXITCODE -ne 0) { throw "Could not read $Path from the saved image." }
    # OneBranch may add lowercase labels alongside Docker's canonical Labels.
    ($json -join "`n") | ConvertFrom-Json -AsHashtable
}
$manifests = @(Read-ImageJson 'manifest.json')
if ($manifests.Count -ne 1 -or @($manifests[0].RepoTags).Count -ne 1 -or
    $manifests[0].Config -cnotmatch '^(?:blobs/sha256/)?[a-f0-9]{64}(?:\.json)?$') {
    throw 'Expected one saved image with one tag and a configuration digest.'
}
$configuration = Read-ImageJson $manifests[0].Config
$labels = $configuration['config']['Labels']
if ($configuration.os -cne 'linux' -or $configuration.architecture -cne 'amd64' -or
    $labels['org.opencontainers.image.version'] -cne $metadata.version -or
    $labels['org.opencontainers.image.revision'] -cne $metadata.sourceVersion -or
    $labels['com.azure.signalr.package.sha256'] -cne $metadata.packageSha256) {
    throw 'The saved image platform, version, source and package must match this build.'
}
$metadata | Add-Member -NotePropertyMembers @{
    archiveFile = $archiveFile
    imageReference = [string] $manifests[0].RepoTags[0]
    archiveSha256 = (Get-FileHash $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
}
$metadata | ConvertTo-Json | Set-Content (Join-Path $OutputDirectory 'container-release.json') -Encoding utf8NoBOM
# Keep the signed helpers from the package artifact unchanged.
Copy-Item (Join-Path $PackageDirectory 'Prepare-EmulatorRelease.ps1') $OutputDirectory
Copy-Item (Join-Path $PackageDirectory 'Verify-EmulatorContainer.ps1') $OutputDirectory
Write-Host "Recorded tested OneBranch image $($metadata.imageReference)."
