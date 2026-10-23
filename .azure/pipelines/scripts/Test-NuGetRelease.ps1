# Exercise release finalization with an in-memory GitHub API; never publish.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$create = Join-Path $PSScriptRoot 'Complete-NuGetRelease.ps1'
$scratch = Join-Path ([IO.Path]::GetTempPath()) "signalr-version-bump-test-$([guid]::NewGuid())"
$credentialKey = 'http.https://github.com/Azure/azure-signalr.extraheader'
$expectedAuthorization = 'basic dGVzdDp0ZXN0'
$versionTemplate = @'
<Project>
  <PropertyGroup>
    <!-- Preserve formatting. -->
    <VersionPrefix>VERSION</VersionPrefix>
  </PropertyGroup>
</Project>
'@
$fixture = @{
    'build/versions/sdk.props' = $versionTemplate.Replace('VERSION', '1.33.2')
    'build/versions/emulator.props' = $versionTemplate.Replace('VERSION', '1.7.0')
    'build/versions/serverless-protocol.props' = $versionTemplate.Replace('VERSION', '1.11.2')
    'version.props' = '<Project><PropertyGroup><VersionSuffix>preview1</VersionSuffix></PropertyGroup></Project>'
}
$emulatorPath = 'build/versions/emulator.props'
$sdkPath = 'build/versions/sdk.props'

function Reset-State {
    $script:state = @{
        BaseFiles = $fixture.Clone()
        BaseSha = 'base-sha'
        BranchFiles = $null
        Branch = $null
        PullRequest = $null
        BranchCreates = 0
        Commits = 0
        PullCreates = 0
        Tags = @(
            @{ ref = 'refs/tags/v1.33.1'; object = @{ type = 'commit'; sha = 'previous-sdk' } },
            @{ ref = 'refs/tags/emulator/v1.6.1'; object = @{ type = 'commit'; sha = 'previous-emulator' } },
            @{ ref = 'refs/tags/serverlessProtocol/v1.11.1'; object = @{ type = 'commit'; sha = 'previous-serverless' } }
        )
        TagCreates = 0
        Release = $null
        ReleaseCreates = 0
        PreviousTag = $null
        AnnotatedTarget = $sourceCommit
        FailAt = ''
    }
}

function Assert-Equal($Actual, $Expected) {
    if ($Actual -cne $Expected) { throw "Expected '$Expected', got '$Actual'." }
}

function Invoke-TestGit {
    & git -C $scratch @args
    if ($LASTEXITCODE -ne 0) { throw "Test git command failed: $args" }
}

function Assert-Rejected([scriptblock] $Action, [string] $Message) {
    try { & $Action }
    catch {
        if ($_.Exception.Message -like "*$Message*") { return }
        throw
    }
    throw "Expected rejection: $Message"
}

function Invoke-RestMethod {
    param($Uri, $Method = 'Get', $Body, $Headers, $ContentType)
    if ($Headers.Authorization -cne $expectedAuthorization) {
        throw 'Checkout authorization was not passed through unchanged.'
    }
    $path = $Uri.Replace('https://api.github.com/repos/Azure/azure-signalr/', '')
    if ($Method -eq 'Get') {
        switch -Wildcard ($path) {
            'pulls?*' {
                # Invoke-RestMethod emits JSON arrays without enumerating them.
                if ($state.PullRequest) { return ,@($state.PullRequest) }
                return ,@()
            }
            'git/ref/heads/dev' { return @{ object = @{ sha = $state.BaseSha } } }
            'git/matching-refs/tags/*' {
                $prefix = 'refs/tags/' + $path.Substring('git/matching-refs/tags/'.Length)
                return ,@($state.Tags | Where-Object { $_.ref.StartsWith($prefix) })
            }
            'git/tags/*' { return @{ object = @{ type = 'commit'; sha = $state.AnnotatedTarget } } }
            'releases/tags/*' {
                if ($state.Release) { return $state.Release }
                $status = if ($state.FailAt -eq 'release-read') { [Net.HttpStatusCode]::Forbidden } else { [Net.HttpStatusCode]::NotFound }
                throw [Microsoft.PowerShell.Commands.HttpResponseException]::new('Simulated release lookup failure.', [Net.Http.HttpResponseMessage]::new($status))
            }
            'git/matching-refs/heads/*' {
                if ($state.Branch) { return ,@(@{ ref = "refs/heads/$($state.Branch)" }) }
                return ,@()
            }
            'contents/*?ref=*' {
                $filePath, $ref = $path.Substring('contents/'.Length) -split '\?ref=', 2
                $files = if ($ref -eq $state.BaseSha) { $state.BaseFiles } elseif ($ref -eq $state.Branch) { $state.BranchFiles }
                if ($null -eq $files -or -not $files.ContainsKey($filePath)) { throw "File $filePath does not exist at $ref." }
                $content = $files[$filePath]
                return @{ content = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($content)); sha = 'file-sha' }
            }
        }
    }
    else {
        $data = $Body | ConvertFrom-Json
        switch -Wildcard ("$Method $path") {
            'Post git/refs' {
                if ($data.ref.StartsWith('refs/tags/')) {
                    Assert-Equal $data.sha $sourceCommit
                    $state.Tags += @{ ref = $data.ref; object = @{ type = 'commit'; sha = $data.sha } }
                    $state.TagCreates++
                    return @{}
                }
                Assert-Equal $data.sha $state.BaseSha
                $state.Branch = $data.ref.Substring('refs/heads/'.Length)
                $state.BranchFiles = $state.BaseFiles.Clone()
                $state.BranchCreates++
                return @{}
            }
            'Post releases/generate-notes' {
                Assert-Equal $data.target_commitish $sourceCommit
                $state.PreviousTag = $data.previous_tag_name
                return @{ body = 'Generated release notes.' }
            }
            'Post releases' {
                if ($state.FailAt -eq 'release') { throw 'Simulated release creation failure.' }
                Assert-Equal $data.target_commitish $sourceCommit
                Assert-Equal $data.draft $false
                Assert-Equal $data.prerelease $false
                $state.Release = @{
                    html_url = 'https://github.com/Azure/azure-signalr/releases/tag/' + $data.tag_name
                    tag_name = $data.tag_name
                    body = $data.body
                    draft = $data.draft
                    prerelease = $data.prerelease
                    make_latest = $data.make_latest
                }
                $state.ReleaseCreates++
                return $state.Release
            }
            'Put contents/*' {
                if ($state.FailAt -eq 'commit') { throw 'Simulated commit failure.' }
                Assert-Equal $data.branch $state.Branch
                $filePath = $path.Substring('contents/'.Length)
                if (-not $state.BranchFiles.ContainsKey($filePath)) { throw "Unexpected file update: $filePath" }
                $state.BranchFiles[$filePath] = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($data.content))
                $state.Commits++
                return @{}
            }
            'Post pulls' {
                if ($state.FailAt -eq 'pull-request') { throw 'Simulated PR creation failure.' }
                Assert-Equal $data.base 'dev'
                Assert-Equal $data.head $state.Branch
                $state.PullRequest = @{ html_url = 'https://github.com/Azure/azure-signalr/pull/123'; state = 'open' }
                $state.PullCreates++
                return $state.PullRequest
            }
        }
    }
    throw "Unexpected API request: $Method $path"
}

try {
    & git init --quiet $scratch
    if ($LASTEXITCODE -ne 0) { throw 'Could not initialize the test checkout.' }
    Invoke-TestGit config --local $credentialKey "AUTHORIZATION: $expectedAuthorization"
    Invoke-TestGit config --local user.name release-test
    Invoke-TestGit config --local user.email release-test@example.invalid
    Invoke-TestGit config --local commit.gpgsign false
    Invoke-TestGit config --local core.autocrlf false
    New-Item -ItemType Directory -Path (Join-Path $scratch 'build/versions') -Force | Out-Null
    foreach ($path in $fixture.Keys) {
        [IO.File]::WriteAllText((Join-Path $scratch $path), $fixture[$path])
    }
    Invoke-TestGit add .
    Invoke-TestGit commit --quiet -m 'Release source'
    $sourceCommit = Invoke-TestGit rev-parse HEAD
    $bumpBranches = @()
    foreach ($case in @(
        @{ Package = 'SDK'; File = $sdkPath; Before = '1.33.2'; After = '1.33.3'; Tag = 'v1.33.2'; Previous = 'v1.33.1' },
        @{ Package = 'Emulator'; File = $emulatorPath; Before = '1.7.0'; After = '1.7.1'; Tag = 'emulator/v1.7.0'; Previous = 'emulator/v1.6.1' },
        @{ Package = 'ServerlessProtocol'; File = 'build/versions/serverless-protocol.props'; Before = '1.11.2'; After = '1.11.3'; Tag = 'serverlessProtocol/v1.11.2'; Previous = 'serverlessProtocol/v1.11.1' }
    )) {
        Reset-State
        & $create -Package $case.Package -RepositoryRoot $scratch
        foreach ($path in $fixture.Keys) {
            $expected = if ($path -eq $case.File) { $fixture[$path].Replace($case.Before, $case.After) } else { $fixture[$path] }
            Assert-Equal $state.BranchFiles[$path] $expected
        }
        Assert-Equal $state.PullCreates 1
        Assert-Equal $state.Release.tag_name $case.Tag
        Assert-Equal $state.PreviousTag $case.Previous
        # Both open and deliberately closed PRs remain unique on retries.
        & $create -Package $case.Package -RepositoryRoot $scratch
        $state.PullRequest.state = 'closed'
        & $create -Package $case.Package -RepositoryRoot $scratch
        Assert-Equal $state.PullCreates 1
        Assert-Equal $state.Commits 1
        Assert-Equal $state.TagCreates 1
        Assert-Equal $state.ReleaseCreates 1

        # Materialize each independent PR from the same release commit.
        Invoke-TestGit checkout --quiet -b $state.Branch $sourceCommit
        foreach ($path in $state.BranchFiles.Keys) {
            [IO.File]::WriteAllText((Join-Path $scratch $path), $state.BranchFiles[$path])
        }
        Invoke-TestGit add .
        Invoke-TestGit commit --quiet -m "Bump $($case.Package)"
        Assert-Equal (Invoke-TestGit diff --name-only $sourceCommit HEAD) $case.File
        $bumpBranches += $state.Branch
        Invoke-TestGit checkout --quiet --detach $sourceCommit
    }

    # All three version PRs must merge without conflicts and retain every bump.
    Invoke-TestGit checkout --quiet -b merged-bumps $sourceCommit
    foreach ($branch in $bumpBranches) { Invoke-TestGit merge --quiet --no-edit $branch }
    Assert-Equal ([IO.File]::ReadAllText((Join-Path $scratch $sdkPath)).Trim()) ($fixture[$sdkPath].Replace('1.33.2', '1.33.3').Trim())
    Assert-Equal ([IO.File]::ReadAllText((Join-Path $scratch $emulatorPath)).Trim()) ($fixture[$emulatorPath].Replace('1.7.0', '1.7.1').Trim())
    $serverlessPath = 'build/versions/serverless-protocol.props'
    Assert-Equal ([IO.File]::ReadAllText((Join-Path $scratch $serverlessPath)).Trim()) ($fixture[$serverlessPath].Replace('1.11.2', '1.11.3').Trim())
    Invoke-TestGit checkout --quiet --detach $sourceCommit

    Reset-State
    $state.BaseFiles[$emulatorPath] = $fixture[$emulatorPath].Replace('1.7.0', '1.8.0')
    & $create -Package Emulator -RepositoryRoot $scratch
    Assert-Equal $state.BranchCreates 0
    Assert-Equal $state.ReleaseCreates 1
    Reset-State
    $state.BaseFiles[$emulatorPath] = $fixture[$emulatorPath].Replace('1.7.0', '1.6.0')
    Assert-Rejected { & $create -Package Emulator -RepositoryRoot $scratch } 'behind the published version'
    Assert-Equal $state.BranchCreates 0

    # Basing the PR on current dev preserves changes made after the release build.
    Reset-State
    $state.BaseFiles[$sdkPath] = $fixture[$sdkPath].Replace('1.33.2', '1.34.0')
    & $create -Package Emulator -RepositoryRoot $scratch
    Assert-Equal $state.BranchFiles[$sdkPath] $state.BaseFiles[$sdkPath]
    Assert-Equal $state.BranchFiles[$emulatorPath] ($fixture[$emulatorPath].Replace('1.7.0', '1.7.1'))

    foreach ($failure in @('release', 'commit', 'pull-request')) {
        Reset-State
        $state.FailAt = $failure
        Assert-Rejected { & $create -Package Emulator -RepositoryRoot $scratch } 'Simulated'
        $state.FailAt = ''
        # Another component's bump reaches dev between failure and retry.
        $state.BaseSha = 'dev-after-sdk-bump'
        $state.BaseFiles[$sdkPath] = $fixture[$sdkPath].Replace('1.33.2', '1.33.3')
        & $create -Package Emulator -RepositoryRoot $scratch
        Assert-Equal $state.BranchFiles[$emulatorPath] ($fixture[$emulatorPath].Replace('1.7.0', '1.7.1'))
        Assert-Equal $state.BranchCreates 1
        Assert-Equal $state.Commits 1
        Assert-Equal $state.PullCreates 1
        Assert-Equal $state.TagCreates 1
        Assert-Equal $state.ReleaseCreates 1
    }

    # Never move an existing tag, including annotated tags, to a different build.
    foreach ($type in @('commit', 'tag')) {
        Reset-State
        $state.Tags += @{ ref = 'refs/tags/emulator/v1.7.0'; object = @{ type = $type; sha = 'different-commit' } }
        $state.AnnotatedTarget = 'different-commit'
        Assert-Rejected { & $create -Package Emulator -RepositoryRoot $scratch } 'different source commit'
        Assert-Equal $state.ReleaseCreates 0
        Assert-Equal $state.TagCreates 0
    }
    Reset-State
    $state.Tags += @{ ref = 'refs/tags/emulator/v1.7.0'; object = @{ type = 'tag'; sha = 'annotated-tag' } }
    & $create -Package Emulator -RepositoryRoot $scratch
    Assert-Equal $state.ReleaseCreates 1
    Assert-Equal $state.TagCreates 0

    # Authentication failures must not be treated as an absent release.
    Reset-State
    $state.FailAt = 'release-read'
    Assert-Rejected { & $create -Package Emulator -RepositoryRoot $scratch } 'Simulated release lookup failure'
    Assert-Equal $state.ReleaseCreates 0
    Assert-Equal $state.PullCreates 0

    Reset-State
    $state.Release = @{ draft = $true; prerelease = $false }
    Assert-Rejected { & $create -Package Emulator -RepositoryRoot $scratch } 'draft or prerelease'
    Assert-Equal $state.ReleaseCreates 0

    Reset-State
    $state.Branch = 'codex/bump-emulator-1.7.1'
    $state.BranchFiles = $fixture.Clone()
    $state.BranchFiles[$emulatorPath] = $fixture[$emulatorPath].Replace('Preserve formatting.', 'Unrelated branch edit.')
    Assert-Rejected { & $create -Package Emulator -RepositoryRoot $scratch } 'contains different changes'
    Assert-Equal $state.Commits 0
    Assert-Equal $state.PullCreates 0

    Reset-State
    $expectedAuthorization = 'Bearer test-only'
    & git -C $scratch config --local $credentialKey "Authorization: $expectedAuthorization"
    if ($LASTEXITCODE -ne 0) { throw 'Could not change test checkout credentials.' }
    & $create -Package Emulator -RepositoryRoot $scratch
    Assert-Equal $state.PullCreates 1

    Reset-State
    & git -C $scratch config --local --unset $credentialKey
    if ($LASTEXITCODE -ne 0) { throw 'Could not clear test checkout credentials.' }
    Assert-Rejected { & $create -Package Emulator -RepositoryRoot $scratch } 'No GitHub authorization header'
    Assert-Equal $state.BranchCreates 0
    Write-Host 'NuGet release finalization tests passed.'
}
finally {
    if (Test-Path -LiteralPath $scratch) {
        $resolvedScratch = (Resolve-Path -LiteralPath $scratch).Path
        if (-not $resolvedScratch.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Test cleanup path is outside the temporary directory.'
        }
        Remove-Item -LiteralPath $resolvedScratch -Recurse -Force
    }
}
