[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $OutputDirectory,
    [Parameter(Mandatory)][string] $BuildNumber,
    [string] $Configuration = 'Release',
    [string] $SignType = 'real'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$groups = [ordered]@{
    sdk = @('Microsoft.Azure.SignalR', 'Microsoft.Azure.SignalR.AspNet', 'Microsoft.Azure.SignalR.Management', 'Microsoft.Azure.SignalR.Protocols')
    serverless = @('Microsoft.Azure.SignalR.Serverless.Protocols')
}

# Both variants use the assemblies already built, tested and signed by this job.
foreach ($kind in @('release', 'preview')) {
    $isFinalBuild = $kind -eq 'release'
    $versionSuffix = if ($isFinalBuild) { 'rtm' } else { "preview1-$BuildNumber" }
    $properties = "-p:BuildNumber=$BuildNumber;VersionSuffix=$versionSuffix;IsFinalBuild=$isFinalBuild;IsTargetMultiFramework=true;SignType=$SignType"
    foreach ($group in $groups.Keys) {
        $destination = Join-Path $OutputDirectory "$kind/$group"
        foreach ($name in $groups[$group]) {
            $project = Join-Path $repoRoot "src/$name/$name.csproj"
            & dotnet pack $project --configuration $Configuration --no-build --no-restore --output $destination `
                --include-symbols --include-source $properties
            if ($LASTEXITCODE -ne 0) { throw "Packing $name ($kind) failed with exit code $LASTEXITCODE." }
        }
    }
}
