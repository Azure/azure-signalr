[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $PackageDirectory,
    [Parameter(Mandatory)][string] $ExpectedVersion
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$packageId = 'Microsoft.Azure.SignalR.Emulator'
$packageDirectory = (Resolve-Path -LiteralPath $PackageDirectory).Path
$package = Join-Path $packageDirectory "$packageId.$ExpectedVersion.nupkg"
$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $entry = $archive.GetEntry("$packageId.nuspec")
    if ($null -eq $entry) { throw 'The emulator package is missing its nuspec.' }
    $reader = [IO.StreamReader]::new($entry.Open())
    try { [xml] $nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ($nuspec.package.metadata.id -ne $packageId -or $nuspec.package.metadata.version -ne $ExpectedVersion) {
        throw 'The packaged ID or version does not match the selected release.'
    }
}
finally {
    $archive.Dispose()
}

$scratch = Join-Path ([IO.Path]::GetTempPath()) "signalr-emulator-package-$([guid]::NewGuid())"
New-Item -ItemType Directory -Path $scratch | Out-Null
$process = $null
$client = $null
try {
    $toolDirectory = Join-Path $scratch 'tool'
    # Install only the package just built, without falling back to a public feed.
    $config = Join-Path $scratch 'NuGet.Config'
    $escapedDirectory = [Security.SecurityElement]::Escape($packageDirectory)
    Set-Content -LiteralPath $config -Value "<configuration><packageSources><clear/><add key=`"package`" value=`"$escapedDirectory`"/></packageSources></configuration>"
    & dotnet tool install $packageId --version $ExpectedVersion --tool-path $toolDirectory --configfile $config --no-cache
    if ($LASTEXITCODE -ne 0) { throw 'Unable to install the emulator package.' }

    $toolName = if ($IsWindows) { 'asrs-emulator.exe' } else { 'asrs-emulator' }
    $stdout = Join-Path $scratch 'stdout.log'
    $stderr = Join-Path $scratch 'stderr.log'
    $startOptions = @{
        FilePath = Join-Path $toolDirectory $toolName
        ArgumentList = @('start', '--ip', '127.0.0.1', '--port', '0')
        WorkingDirectory = $scratch
        RedirectStandardOutput = $stdout
        RedirectStandardError = $stderr
        PassThru = $true
    }
    if ($IsWindows) { $startOptions.WindowStyle = 'Hidden' }
    $process = Start-Process @startOptions
    $client = [Net.Http.HttpClient]::new()
    $client.Timeout = [TimeSpan]::FromSeconds(2)
    $healthy = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if ($process.HasExited) { break }
        $log = Get-Content -LiteralPath $stdout -Raw -ErrorAction SilentlyContinue
        if ($log -match 'Endpoint=(http://127\.0\.0\.1);Port=(\d+);') {
            $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Head, "$($Matches[1]):$($Matches[2])/api/health")
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
        }
        Start-Sleep -Seconds 1
    }
    if (-not $healthy) {
        Get-Content -LiteralPath $stdout, $stderr -ErrorAction SilentlyContinue
        throw 'The packaged emulator did not become healthy.'
    }
}
finally {
    if ($null -ne $client) { $client.Dispose() }
    if ($null -ne $process) {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
        $process.Dispose()
    }
    $resolvedScratch = (Resolve-Path -LiteralPath $scratch).Path
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if (-not $resolvedScratch.StartsWith($tempRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a directory outside the temporary directory: $resolvedScratch"
    }
    Remove-Item -LiteralPath $resolvedScratch -Recurse -Force
}

Write-Host "Validated $packageId $ExpectedVersion"
