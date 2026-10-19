[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $PackageDirectory,
    [Parameter(Mandatory)][string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-Docker {
    & docker @args
    if ($LASTEXITCODE -ne 0) { throw "docker $($args[0]) failed with exit code $LASTEXITCODE." }
}

$packageRoot = (Resolve-Path $PackageDirectory).Path
& (Join-Path $PSScriptRoot 'Prepare-EmulatorRelease.ps1') -ArtifactDirectory $packageRoot -Action package
$metadata = Get-Content (Join-Path $packageRoot 'package-release.json') -Raw | ConvertFrom-Json
$image = "signalr-emulator-build:$($metadata.buildId)"
Invoke-Docker build --platform linux/amd64 --tag $image `
    --build-arg "EMULATOR_VERSION=$($metadata.version)" --build-arg EMULATOR_FEED=/context/release `
    --label "org.opencontainers.image.revision=$($metadata.sourceVersion)" `
    --label "com.azure.signalr.package.sha256=$($metadata.packageSha256)" $packageRoot

$containerId = $null
$client = [Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(2)
try {
    $containerId = (Invoke-Docker run --detach --publish 127.0.0.1::8888 $image).Trim()
    $port = (Invoke-Docker inspect --format '{{(index (index .NetworkSettings.Ports "8888/tcp") 0).HostPort}}' $containerId).Trim()
    $healthy = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Head, "http://127.0.0.1:$port/api/health")
        $response = $null
        try {
            $response = $client.SendAsync($request).GetAwaiter().GetResult()
            if ($response.IsSuccessStatusCode) { $healthy = $true; break }
        }
        catch [Net.Http.HttpRequestException] { }
        catch [Threading.Tasks.TaskCanceledException] { }
        finally {
            if ($null -ne $response) { $response.Dispose() }
            $request.Dispose()
        }
        Start-Sleep -Seconds 1
    }
    if (-not $healthy) {
        Invoke-Docker logs $containerId
        throw 'The emulator container did not become healthy.'
    }
}
finally {
    $client.Dispose()
    if ($containerId) { Invoke-Docker rm --force $containerId | Out-Null }
}

$configuration = (Invoke-Docker image inspect $image) -join "`n" | ConvertFrom-Json
if ($configuration[0].Os -cne 'linux' -or $configuration[0].Architecture -cne 'amd64' -or
    $configuration[0].Config.Labels.'org.opencontainers.image.version' -cne $metadata.version -or
    $configuration[0].Config.Labels.'org.opencontainers.image.revision' -cne $metadata.sourceVersion -or
    $configuration[0].Config.Labels.'com.azure.signalr.package.sha256' -cne $metadata.packageSha256) {
    throw 'The image platform, version, source and package must match this build.'
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $output | Out-Null
$archiveFile = "signalr-emulator.$($metadata.version).linux-amd64.tar"
$archivePath = Join-Path $output $archiveFile
Invoke-Docker image save --output $archivePath $image
$metadata | Add-Member -NotePropertyMembers @{
    archiveFile = $archiveFile
    imageReference = $image
    archiveSha256 = (Get-FileHash $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
}
$metadata | ConvertTo-Json | Set-Content (Join-Path $output 'container-release.json') -Encoding utf8NoBOM
Copy-Item (Join-Path $packageRoot 'Prepare-EmulatorRelease.ps1') $output
Copy-Item (Join-Path $packageRoot 'Verify-EmulatorContainer.ps1') $output
Write-Host "Saved tested emulator image $image."
