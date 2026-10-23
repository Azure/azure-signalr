[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('SDK', 'Emulator', 'ServerlessProtocol')][string] $Package,
    [string] $RepositoryRoot = (Join-Path $PSScriptRoot '../../..')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Reuse the GitHub connection credentials retained by checkout: self.
$repoRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$authHeader = & git -C $repoRoot config --local --get-urlmatch http.extraheader 'https://github.com/Azure/azure-signalr'
if ($LASTEXITCODE -ne 0 -or $authHeader -notmatch '^AUTHORIZATION:\s*(\S.*)$') {
    throw 'No GitHub authorization header found in the checkout. Enable checkout.persistCredentials and authorize its GitHub connection to write contents and pull requests.'
}
$authorization = $Matches[1].Trim()

$versionPath = switch ($Package) {
    'Emulator' { 'build/versions/emulator.props' }
    'ServerlessProtocol' { 'build/versions/serverless-protocol.props' }
    'SDK' { 'build/versions/sdk.props' }
}
$versionFile = Join-Path $repoRoot $versionPath

function Get-VersionMatch([string] $Content) {
    $versionMatches = [regex]::Matches($Content, '<VersionPrefix>(?<version>[^<]+)</VersionPrefix>')
    if ($versionMatches.Count -ne 1 -or $versionMatches[0].Groups['version'].Value -notmatch '^\d+\.\d+\.\d+$') {
        throw "Expected one major.minor.patch VersionPrefix for $Package."
    }
    return $versionMatches[0].Groups['version']
}

# checkout: self supplies the exact source used to build the published package.
$releasedVersion = [version](Get-VersionMatch (Get-Content -LiteralPath $VersionFile -Raw)).Value
$sourceCommit = & git -C $repoRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Could not determine the published package source commit.' }
$nextVersion = '{0}.{1}.{2}' -f $releasedVersion.Major, $releasedVersion.Minor, ($releasedVersion.Build + 1)
$branch = "codex/bump-$($Package.ToLowerInvariant())-$nextVersion"
$repository = 'https://api.github.com/repos/Azure/azure-signalr'
$api = @{
    Headers = @{
        Authorization = $authorization
        Accept = 'application/vnd.github+json'
        'X-GitHub-Api-Version' = '2022-11-28'
        'User-Agent' = 'azure-signalr-release'
    }
    ContentType = 'application/json; charset=utf-8'
}

# Keep each component's existing tag namespace and pin it to the release build.
$tagPrefix = switch ($Package) {
    'SDK' { 'v' }
    'Emulator' { 'emulator/v' }
    'ServerlessProtocol' { 'serverlessProtocol/v' }
}
$releaseName = switch ($Package) {
    'SDK' { "SDK v$releasedVersion" }
    'Emulator' { "Emulator v$releasedVersion" }
    'ServerlessProtocol' { "Serverless protocol v$releasedVersion" }
}
$tag = "$tagPrefix$releasedVersion"
$tagRefs = Invoke-RestMethod @api -Uri "$repository/git/matching-refs/tags/$tagPrefix"
$tagObject = $tagRefs | Where-Object { $_.ref -ceq "refs/tags/$tag" } | Select-Object -ExpandProperty object
if ($tagObject) {
    # Existing annotated tags must resolve to the same source commit too.
    while ($tagObject.type -eq 'tag') {
        $annotatedTag = Invoke-RestMethod @api -Uri "$repository/git/tags/$($tagObject.sha)"
        $tagObject = $annotatedTag.object
    }
    if ($tagObject.type -ne 'commit' -or $tagObject.sha -cne $sourceCommit) {
        throw "Tag $tag points to a different source commit; it will not be moved."
    }
}
else {
    $body = @{ ref = "refs/tags/$tag"; sha = $sourceCommit } | ConvertTo-Json
    Invoke-RestMethod @api -Method Post -Uri "$repository/git/refs" -Body $body | Out-Null
}

$release = $null
try {
    $release = Invoke-RestMethod @api -Uri "$repository/releases/tags/$([Uri]::EscapeDataString($tag))"
}
catch [Microsoft.PowerShell.Commands.HttpResponseException] {
    if ($_.Exception.Response.StatusCode -ne [Net.HttpStatusCode]::NotFound) { throw }
}
if ($release) {
    if ($release.draft -or $release.prerelease) {
        throw "Release $tag already exists as a draft or prerelease; review it manually."
    }
    Write-Host "GitHub release already exists: $($release.html_url)"
}
else {
    # Compare with the previous version of this component, not another package.
    $previousTag = $tagRefs | ForEach-Object {
        $name = $_.ref.Substring('refs/tags/'.Length)
        if ($name -cmatch ('^' + [regex]::Escape($tagPrefix) + '(?<version>\d+\.\d+\.\d+)$') -and [version]$Matches.version -lt $releasedVersion) {
            [pscustomobject]@{ Name = $name; Version = [version]$Matches.version }
        }
    } | Sort-Object Version -Descending | Select-Object -First 1
    $releaseBody = "$releaseName is available on NuGet.org."
    if ($previousTag) {
        $body = @{ tag_name = $tag; target_commitish = $sourceCommit; previous_tag_name = $previousTag.Name } | ConvertTo-Json
        $notes = Invoke-RestMethod @api -Method Post -Uri "$repository/releases/generate-notes" -Body $body
        $releaseBody += "`n`n$($notes.body)"
    }
    $body = @{
        tag_name = $tag
        target_commitish = $sourceCommit
        name = $releaseName
        body = $releaseBody
        draft = $false
        prerelease = $false
        make_latest = if ($Package -eq 'SDK') { 'legacy' } else { 'false' }
    } | ConvertTo-Json
    $release = Invoke-RestMethod @api -Method Post -Uri "$repository/releases" -Body $body
    Write-Host "Created GitHub release: $($release.html_url)"
}

# Include closed PRs: retries must not reopen a PR that someone deliberately closed.
$pullRequests = Invoke-RestMethod @api -Uri "$repository/pulls?state=all&base=dev&head=Azure:$branch"
if ($pullRequests.Count -gt 0) {
    Write-Host "Version bump PR already exists: $($pullRequests[0].html_url)"
    return
}

$baseRef = Invoke-RestMethod @api -Uri "$repository/git/ref/heads/dev"
$baseFile = Invoke-RestMethod @api -Uri "$repository/contents/${versionPath}?ref=$($baseRef.object.sha)"
$content = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($baseFile.content))
$current = Get-VersionMatch $content
if ([version]$current.Value -gt $releasedVersion) {
    Write-Host "$Package is already at $($current.Value) on dev; no version bump is needed for release $releasedVersion."
    return
}
if ([version]$current.Value -ne $releasedVersion) {
    throw "$Package on dev ($($current.Value)) is behind the published version $releasedVersion; review $versionPath manually."
}

# Update only this component's version file, preserving its formatting.
$updated = $content.Remove($current.Index, $current.Length).Insert($current.Index, $nextVersion)
$title = "Bump $Package version to $nextVersion"
$branchRefs = Invoke-RestMethod @api -Uri "$repository/git/matching-refs/heads/$branch"
$branchRefs = @($branchRefs | Where-Object { $_.ref -eq "refs/heads/$branch" })
if ($branchRefs.Count -eq 0) {
    $body = @{ ref = "refs/heads/$branch"; sha = $baseRef.object.sha } | ConvertTo-Json
    Invoke-RestMethod @api -Method Post -Uri "$repository/git/refs" -Body $body | Out-Null
}

# Resume after a partial failure without overwriting an existing branch's edits.
$branchFile = Invoke-RestMethod @api -Uri "$repository/contents/${versionPath}?ref=$branch"
$branchContent = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($branchFile.content))
if ($branchContent -ceq $content) {
    $body = @{
        message = $title
        content = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($updated))
        sha = $branchFile.sha
        branch = $branch
    } | ConvertTo-Json
    Invoke-RestMethod @api -Method Put -Uri "$repository/contents/$versionPath" -Body $body | Out-Null
}
elseif ($branchContent -cne $updated) {
    throw "Branch $branch contains different changes; review it manually before retrying."
}

$body = @{
    title = $title
    head = $branch
    base = 'dev'
    body = "After publishing $Package $releasedVersion to NuGet.org, increment its patch version to $nextVersion for subsequent development. Only $versionPath changes."
} | ConvertTo-Json
$pullRequest = Invoke-RestMethod @api -Method Post -Uri "$repository/pulls" -Body $body
Write-Host "Created version bump PR: $($pullRequest.html_url)"
