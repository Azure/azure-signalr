[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $OciLayout,
    [Parameter(Mandatory)][string] $ImageReference,
    [Parameter(Mandatory)][string] $TargetImage,
    [Parameter(Mandatory)][string] $OrasPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$tag = $ImageReference.Split(':')[-1]
$local = & $OrasPath manifest fetch --descriptor --oci-layout "${OciLayout}:$tag"
if ($LASTEXITCODE -ne 0) { throw 'Could not read the local image manifest.' }
$remote = & $OrasPath manifest fetch --descriptor $TargetImage
if ($LASTEXITCODE -ne 0) { throw "Could not read the published image manifest for $TargetImage." }
$expected = ($local -join "`n" | ConvertFrom-Json).digest
$actual = ($remote -join "`n" | ConvertFrom-Json).digest
if ($expected -cnotmatch '^sha256:[a-f0-9]{64}$' -or $actual -cne $expected) {
    throw "Published image digest '$actual' does not match the local image digest '$expected'."
}
Write-Host "Verified ${TargetImage}: $actual"
