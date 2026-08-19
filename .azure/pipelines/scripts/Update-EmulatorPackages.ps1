param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Expand', 'Repack')]
    [string]$Action,

    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory,

    [Parameter(Mandatory = $true)]
    [string]$ExpandedRoot
)

$ErrorActionPreference = 'Stop'
$packages = Get-ChildItem -LiteralPath $PackageDirectory -Filter 'Microsoft.Azure.SignalR.Emulator*.nupkg'

if ($packages.Count -eq 0) {
    throw "No Emulator packages found in '$PackageDirectory'."
}

if ($Action -eq 'Expand') {
    New-Item -ItemType Directory -Path $ExpandedRoot -Force | Out-Null

    foreach ($package in $packages) {
        $packageDirectory = Join-Path $ExpandedRoot $package.Name
        [System.IO.Compression.ZipFile]::ExtractToDirectory($package.FullName, $packageDirectory)
    }

    return
}

foreach ($package in $packages) {
    $packageDirectory = Join-Path $ExpandedRoot $package.Name
    Remove-Item -LiteralPath $package.FullName -Force
    [System.IO.Compression.ZipFile]::CreateFromDirectory($packageDirectory, $package.FullName)
}
