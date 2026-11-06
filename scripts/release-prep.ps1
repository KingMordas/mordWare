<#
.SYNOPSIS
    Checks a release run before anything is built, and writes its release notes.

.DESCRIPTION
    Used by .github/workflows/release.yml, and testable locally (-SelfTest).

    Tag mode (-Tag vX.Y.Z), a real release:
      1. The tag has the form vX.Y.Z (digits only, no pre-release or build suffix).
      2. The tagged commit (-Sha) is on main (git merge-base --is-ancestor <sha> <MainRef>).
      3. CHANGELOG.md has a "## [X.Y.Z]" section.
      The version is X.Y.Z.

    Dry-run mode (-DryRun -RunNumber <n>): the version is 0.1.0-dryrun.<n> (vpk refuses versions below 0.0.1, and any 0.0.x pre-release sorts below 0.0.1; it still sorts below every real release) and the notes use the
    "## [Unreleased]" section, so the dry run shows the exact notes a release would get. Nothing is
    published in this mode (the workflow's publish job never runs for it).

    The notes file holds the changelog section, then the license statement and the link to the source
    of exactly that version (the tag, or the commit for a dry run).

    Writes "version=<version>" to $env:GITHUB_OUTPUT when set, and always prints it.
    Exit codes: 0 = ready, 1 = refused (the message names the reason).

.EXAMPLE
    .\scripts\release-prep.ps1 -Tag v1.0.0 -Sha (git rev-parse HEAD) -NotesFile notes.md

.EXAMPLE
    .\scripts\release-prep.ps1 -DryRun -RunNumber 7 -Sha (git rev-parse HEAD) -NotesFile notes.md

.EXAMPLE
    .\scripts\release-prep.ps1 -SelfTest
#>
[CmdletBinding(DefaultParameterSetName = 'Tag')]
param(
    [Parameter(ParameterSetName = 'Tag', Mandatory = $true)][string]$Tag,
    [Parameter(ParameterSetName = 'DryRun', Mandatory = $true)][switch]$DryRun,
    [Parameter(ParameterSetName = 'DryRun', Mandatory = $true)][int]$RunNumber,
    [Parameter(ParameterSetName = 'Tag', Mandatory = $true)]
    [Parameter(ParameterSetName = 'DryRun', Mandatory = $true)][string]$Sha,
    [Parameter(ParameterSetName = 'Tag', Mandatory = $true)]
    [Parameter(ParameterSetName = 'DryRun', Mandatory = $true)][string]$NotesFile,
    [Parameter(ParameterSetName = 'Tag')]
    [Parameter(ParameterSetName = 'DryRun')][string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [Parameter(ParameterSetName = 'Tag')][string]$MainRef = 'origin/main',
    [Parameter(ParameterSetName = 'SelfTest', Mandatory = $true)][switch]$SelfTest
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$RepositoryUrl = 'https://github.com/KingMordas/mordWare'

function Get-ChangelogSection {
    # The body of "## [<Name>]" up to the next "## [" heading, or $null when there is no such section.
    param([string]$Changelog, [string]$Name)
    $lines = [System.IO.File]::ReadAllLines($Changelog)
    $start = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match ('^## \[' + [regex]::Escape($Name) + '\](\s|$)')) { $start = $i + 1; break }
    }
    if ($start -lt 0) { return $null }
    $body = @()
    for ($i = $start; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^## \[') { break }
        $body += $lines[$i]
    }
    return (($body -join "`n").Trim())
}

function Invoke-Prep {
    # Returns the version; throws with the reason when the run must not continue.
    param([string]$Mode, [string]$TagName, [int]$Run, [string]$Commit, [string]$Notes, [string]$Root, [string]$Main)
    if ($Commit -notmatch '^[0-9a-f]{40}$') { throw "Sha '$Commit' is not a full commit id." }

    if ($Mode -eq 'Tag') {
        if ($TagName -notmatch '^v(\d+\.\d+\.\d+)$') { throw "Tag '$TagName' is not of the form vX.Y.Z." }
        $version = $Matches[1]
        & git -C $Root merge-base --is-ancestor $Commit $Main 2>$null
        if ($LASTEXITCODE -ne 0) { throw "The tagged commit $Commit is not on main ($Main): releases come only from main." }
        $section = Get-ChangelogSection (Join-Path $Root 'CHANGELOG.md') $version
        if ($null -eq $section) { throw "CHANGELOG.md has no '## [$version]' section: add it before tagging." }
        $sourceRef = "v$version"
    }
    else {
        $version = "0.1.0-dryrun.$Run"
        $section = Get-ChangelogSection (Join-Path $Root 'CHANGELOG.md') 'Unreleased'
        if ($null -eq $section) { throw "CHANGELOG.md has no '## [Unreleased]' section." }
        $sourceRef = $Commit
    }

    $text = $section + "`n`n---`n`n" +
        "mordWare is licensed GPL-3.0-only with an additional permission (LICENSE-EXCEPTION.txt). " +
        "Source code for this version: $RepositoryUrl/tree/$sourceRef`n"
    [System.IO.File]::WriteAllText($Notes, $text, (New-Object System.Text.UTF8Encoding $false))
    return $version
}

function Invoke-SelfTest {
    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ('mordware-release-selftest-' + [guid]::NewGuid().ToString('N'))
    $script:Failures = 0
    function Check([string]$What, [bool]$Ok) {
        if ($Ok) { Write-Host "  ok   $What" -ForegroundColor Green } else { Write-Host "  FAIL $What" -ForegroundColor Red; $script:Failures++ }
    }
    function Refused([scriptblock]$Block, [string]$Expect) {
        try { & $Block | Out-Null; return $false } catch { return $_.Exception.Message.Contains($Expect) }
    }
    try {
        New-Item -ItemType Directory -Path $tmp | Out-Null
        $git = { param([string[]]$a) & git -C $tmp @a 2>&1 | Out-Null; if ($LASTEXITCODE -ne 0) { throw "git $a failed" } }
        & $git @('init', '-q', '-b', 'main')
        & $git @('config', 'user.name', 'Self Test')
        & $git @('config', 'user.email', ('selftest' + '@users.noreply.github.com'))
        & $git @('config', 'commit.gpgsign', 'false')
        $changelog = "# Changelog`n`n## [Unreleased]`n`n- Next thing.`n`n## [1.0.0]`n`n### Added`n`n- First release.`n"
        [System.IO.File]::WriteAllText((Join-Path $tmp 'CHANGELOG.md'), $changelog)
        & $git @('add', '-A'); & $git @('commit', '-q', '-m', 'chore: first')
        $onMain = (& git -C $tmp rev-parse HEAD).Trim()
        & $git @('checkout', '-q', '-b', 'side')
        [System.IO.File]::WriteAllText((Join-Path $tmp 'side.txt'), 'x')
        & $git @('add', '-A'); & $git @('commit', '-q', '-m', 'chore: side')
        $offMain = (& git -C $tmp rev-parse HEAD).Trim()
        & $git @('checkout', '-q', 'main')
        $notes = Join-Path $tmp 'notes.md'

        $v = Invoke-Prep 'Tag' 'v1.0.0' 0 $onMain $notes $tmp 'main'
        $text = [System.IO.File]::ReadAllText($notes)
        Check 'tag on main is accepted, version from the tag' ($v -eq '1.0.0')
        Check 'notes hold the changelog section' ($text.StartsWith('### Added') -and $text.Contains('- First release.') -and -not $text.Contains('Next thing'))
        Check 'notes hold the license line and the tag source link' ($text.Contains('licensed GPL-3.0-only with an additional permission (LICENSE-EXCEPTION.txt)') -and $text.Contains("$RepositoryUrl/tree/v1.0.0"))
        Check 'tag off main is refused' (Refused { Invoke-Prep 'Tag' 'v1.0.0' 0 $offMain $notes $tmp 'main' } 'not on main')
        Check 'missing changelog section is refused and named' (Refused { Invoke-Prep 'Tag' 'v1.1.0' 0 $onMain $notes $tmp 'main' } "'## [1.1.0]'")
        Check 'tag v1.0 is refused' (Refused { Invoke-Prep 'Tag' 'v1.0' 0 $onMain $notes $tmp 'main' } 'vX.Y.Z')
        Check 'tag v1.0.0-rc.1 is refused' (Refused { Invoke-Prep 'Tag' 'v1.0.0-rc.1' 0 $onMain $notes $tmp 'main' } 'vX.Y.Z')
        Check 'a short sha is refused' (Refused { Invoke-Prep 'Tag' 'v1.0.0' 0 'abc123' $notes $tmp 'main' } 'full commit id')

        $v = Invoke-Prep 'DryRun' '' 42 $onMain $notes $tmp 'main'
        $text = [System.IO.File]::ReadAllText($notes)
        Check 'dry run version is 0.1.0-dryrun.<run>' ($v -eq '0.1.0-dryrun.42')
        Check 'dry run notes use Unreleased and link the commit' ($text.Contains('- Next thing.') -and -not $text.Contains('First release') -and $text.Contains("$RepositoryUrl/tree/$onMain"))
    }
    finally {
        if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Recurse -Force }
    }
    Write-Host "release-prep self-test: $($script:Failures) failure(s)"
    return $script:Failures
}

if ($SelfTest) {
    exit ([int]((Invoke-SelfTest) -gt 0))
}

try {
    $mode = $PSCmdlet.ParameterSetName
    $version = Invoke-Prep $mode $(if ($mode -eq 'Tag') { $Tag } else { '' }) $(if ($mode -eq 'DryRun') { $RunNumber } else { 0 }) $Sha $NotesFile $RepoRoot $(if ($mode -eq 'Tag') { $MainRef } else { '' })
    Write-Host "version=$version"
    if ($env:GITHUB_OUTPUT) { Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "version=$version" }
    exit 0
}
catch {
    Write-Host "release-prep: refused: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
