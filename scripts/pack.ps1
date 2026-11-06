<#
.SYNOPSIS
    Packs one edition of mordWare into a per-user Velopack installer and full package.

.DESCRIPTION
    1. Publishes the app self-contained for win-x64: Release for Prod, Debug for Dev (the build fixes
       the edition, guardrail 7), deterministic and path-free (ContinuousIntegrationBuild).
    2. Copies the legal texts into the publish folder: LICENSE, LICENSE-EXCEPTION.txt and
       THIRD-PARTY-NOTICES.txt from the repository, and the upstream third-party notices of the .NET
       runtime and the Windows App SDK, byte for byte from their NuGet packages, into
       third-party-notices\. Refuses to continue if one is missing.
    3. Runs vpk pack with the edition's packId and title, the default channel (win), a Start menu
       shortcut only, no portable zip and no delta package.
    4. Runs the package scan: scripts\check-public-safety.ps1 -Package on the output.

    Output (gitignored): <OutputDir>\publish (cleaned on every run) and <OutputDir>\releases, which
    keeps earlier versions so a newer installer can be checked over an older install.

    Prod is packed by the release workflow (.github/workflows/release.yml). Dev is packed only locally,
    for installer and upgrade checks, and is never published. Never run a Prod Setup.exe on the
    owner's PC: installer checks use Dev only.

    Velopack's Setup.exe fails when started within about 2 seconds of the previous one: wait a few
    seconds before installing a newer version over an older one.

    Requires PowerShell 7, the .NET SDK of global.json and `dotnet tool restore` (vpk).

.PARAMETER Edition
    Prod or Dev.

.PARAMETER Version
    The package version (SemVer 2): the tag's version for a release, 0.1.0-dryrun.<run> for the dry
    run, 0.0.<n> for local checks.

.PARAMETER OutputDir
    Where to write; default artifacts\pack\<Edition> under the repository.

.PARAMETER SelfTest
    Checks the edition name guard and that its names match MordWare.Core's EditionProfile. Packs nothing.

.EXAMPLE
    .\scripts\pack.ps1 -Edition Dev -Version 0.0.1

.EXAMPLE
    .\scripts\pack.ps1 -SelfTest
#>
[CmdletBinding(DefaultParameterSetName = 'Pack')]
param(
    [Parameter(ParameterSetName = 'Pack', Mandatory = $true)][ValidateSet('Prod', 'Dev')][string]$Edition,
    [Parameter(ParameterSetName = 'Pack', Mandatory = $true)][string]$Version,
    [Parameter(ParameterSetName = 'Pack')][string]$OutputDir,
    [Parameter(ParameterSetName = 'SelfTest', Mandatory = $true)][switch]$SelfTest
)

#Requires -Version 7.0
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$AppFolder = Join-Path $RepoRoot 'src\MordWare.App'

# The edition names, as in MordWare.Core.Editions.EditionProfile (checked by -SelfTest).
$EditionNames = @{
    Prod = @{ PackId = 'MordWare'; Title = 'mordWare'; Configuration = 'Release'; Icon = 'Assets\AppIcon.ico' }
    Dev = @{ PackId = 'MordWare.Dev'; Title = 'mordWare (DEV)'; Configuration = 'Debug'; Icon = 'Assets\Dev\AppIcon.ico' }
}

function Get-EditionNames {
    # The names of one edition, refused unless they are exactly the guarded pair: a Dev pack can never
    # use the PROD identity, and the reverse.
    param([string]$Name)
    if (-not $EditionNames.ContainsKey($Name)) { throw "Unknown edition '$Name'." }
    $e = $EditionNames[$Name]
    Assert-EditionPair $Name $e.PackId $e.Title $e.Configuration
    return $e
}

function Assert-EditionPair {
    param([string]$Name, [string]$PackId, [string]$Title, [string]$Configuration)
    $ok = switch ($Name) {
        'Prod' { $PackId -ceq 'MordWare' -and $Title -ceq 'mordWare' -and $Configuration -ceq 'Release' }
        'Dev' { $PackId -ceq 'MordWare.Dev' -and $Title -ceq 'mordWare (DEV)' -and $Configuration -ceq 'Debug' }
        default { $false }
    }
    if (-not $ok) { throw "Refusing edition '$Name' with packId '$PackId', title '$Title', configuration '$Configuration'." }
}

function Invoke-SelfTest {
    $script:SelfTestFailures = 0
    function Check([string]$What, [bool]$Ok) {
        if ($Ok) { Write-Host "  ok   $What" -ForegroundColor Green }
        else { Write-Host "  FAIL $What" -ForegroundColor Red; $script:SelfTestFailures++ }
    }
    function Refuses([scriptblock]$Block) { try { & $Block; return $false } catch { return $true } }

    $dev = Get-EditionNames 'Dev'
    $prod = Get-EditionNames 'Prod'
    Check 'Dev never yields the PROD packId' ($dev.PackId -cne 'MordWare' -and $dev.Title -cne 'mordWare')
    Check 'Prod never yields the DEV packId' ($prod.PackId -cne 'MordWare.Dev' -and $prod.Title -cne 'mordWare (DEV)')
    Check 'Dev is a Debug build, Prod a Release build' ($dev.Configuration -eq 'Debug' -and $prod.Configuration -eq 'Release')
    Check 'guard refuses Dev with the PROD packId' (Refuses { Assert-EditionPair 'Dev' 'MordWare' 'mordWare (DEV)' 'Debug' })
    Check 'guard refuses Prod with the DEV packId' (Refuses { Assert-EditionPair 'Prod' 'MordWare.Dev' 'mordWare' 'Release' })
    Check 'guard refuses Prod built as Debug' (Refuses { Assert-EditionPair 'Prod' 'MordWare' 'mordWare' 'Debug' })
    Check 'guard refuses an unknown edition' (Refuses { Get-EditionNames 'Staging' })

    # The same names as the app's own table.
    $profile = Get-Content -Raw (Join-Path $RepoRoot 'src\MordWare.Core\Editions\EditionProfile.cs')
    Check 'Prod names match EditionProfile.Prod' ($profile.Contains('new(AppEnvironment.Prod, "MordWare", "mordWare", "mordWare Data")'))
    Check 'Dev names match EditionProfile.Dev' ($profile.Contains('new(AppEnvironment.Dev, "MordWare.Dev", "mordWare (DEV)", "mordWare Data (DEV)")'))

    Write-Host "pack self-test: $($script:SelfTestFailures) failure(s)"
    return $script:SelfTestFailures
}

function Find-PackageFile {
    # A file inside a restored package, located through the App's project.assets.json.
    param([object]$Assets, [string]$PackageId, [string]$RelativePath)
    $library = $Assets.libraries.PSObject.Properties | Where-Object { $_.Name -like "$PackageId/*" } | Select-Object -First 1
    if (-not $library) { throw "Package $PackageId is not restored for the App." }
    foreach ($folder in $Assets.packageFolders.PSObject.Properties.Name) {
        $candidate = Join-Path (Join-Path $folder $library.Value.path) $RelativePath
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    throw "$RelativePath not found in package $($library.Name)."
}

function Find-RuntimePackFile {
    # A file of the .NET runtime pack the publish used (its version is in the app's deps.json).
    param([string]$PublishDir, [string]$RelativePath)
    $deps = Get-Content -Raw (Join-Path $PublishDir 'MordWare.App.deps.json')
    if ($deps -notmatch '"runtimepack\.Microsoft\.NETCore\.App\.Runtime\.win-x64/([^"]+)"') { throw 'Runtime pack version not found in MordWare.App.deps.json.' }
    $version = $Matches[1]
    $nuget = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget\packages' }
    $roots = @((Join-Path $nuget "microsoft.netcore.app.runtime.win-x64\$version"))
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnet) { $roots += Join-Path (Split-Path -Parent $dotnet.Source) "packs\Microsoft.NETCore.App.Runtime.win-x64\$version" }
    foreach ($r in $roots) {
        $candidate = Join-Path $r $RelativePath
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    throw "$RelativePath of the .NET runtime pack $version not found."
}

function Invoke-Checked([string]$What, [scriptblock]$Block) {
    & $Block
    if ($LASTEXITCODE -ne 0) { throw "$What failed with exit code $LASTEXITCODE." }
}

if ($SelfTest) {
    exit ([int]((Invoke-SelfTest) -gt 0))
}

# --- Pack -----------------------------------------------------------------------------------------
if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw "Version '$Version' is not a SemVer 2 version." }
$names = Get-EditionNames $Edition
if (-not $OutputDir) { $OutputDir = Join-Path $RepoRoot "artifacts\pack\$Edition" }
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
$publish = Join-Path $OutputDir 'publish'
$releases = Join-Path $OutputDir 'releases'

Write-Host "==> Packing $($names.Title) $Version ($($names.Configuration), packId $($names.PackId))" -ForegroundColor Cyan

if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
New-Item -ItemType Directory -Force -Path $publish, $releases | Out-Null

# 1. Publish. No PDBs in a package, for either edition (vpk would drop them anyway).
Push-Location $RepoRoot
try {
    Invoke-Checked 'dotnet publish' {
        dotnet publish (Join-Path $AppFolder 'MordWare.App.csproj') -c $names.Configuration -r win-x64 -o $publish `
            "-p:Version=$Version" -p:ContinuousIntegrationBuild=true -p:DebugType=none --nologo -v:minimal
    }
}
finally { Pop-Location }

# 2. Legal texts.
$assets = Get-Content -Raw (Join-Path $AppFolder 'obj\project.assets.json') | ConvertFrom-Json
$legal = @(
    @{ From = (Join-Path $RepoRoot 'LICENSE'); To = 'LICENSE' },
    @{ From = (Join-Path $RepoRoot 'LICENSE-EXCEPTION.txt'); To = 'LICENSE-EXCEPTION.txt' },
    @{ From = (Join-Path $RepoRoot 'THIRD-PARTY-NOTICES.txt'); To = 'THIRD-PARTY-NOTICES.txt' },
    @{ From = (Find-PackageFile $assets 'Microsoft.WindowsAppSDK.Base' 'NOTICE.txt'); To = 'third-party-notices\Microsoft.WindowsAppSDK.Base\NOTICE.txt' },
    @{ From = (Find-PackageFile $assets 'Microsoft.WindowsAppSDK.WinUI' 'NOTICE.txt'); To = 'third-party-notices\Microsoft.WindowsAppSDK.WinUI\NOTICE.txt' },
    @{ From = (Find-RuntimePackFile $publish 'THIRD-PARTY-NOTICES.TXT'); To = 'third-party-notices\Microsoft.NETCore.App.Runtime\THIRD-PARTY-NOTICES.TXT' }
)
foreach ($l in $legal) {
    if (-not (Test-Path -LiteralPath $l.From)) { throw "Legal text missing: $($l.To). Nothing is packed without it." }
    $target = Join-Path $publish $l.To
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    Copy-Item -LiteralPath $l.From -Destination $target
}
foreach ($l in $legal) {
    if (-not (Test-Path -LiteralPath (Join-Path $publish $l.To))) { throw "Legal text missing in the publish folder: $($l.To)." }
}

# 3. Velopack.
Push-Location $RepoRoot
try {
    Invoke-Checked 'dotnet tool restore' { dotnet tool restore }
    Invoke-Checked 'vpk pack' {
        dotnet vpk pack --packId $names.PackId --packVersion $Version --packTitle $names.Title --packAuthors 'mordWare' `
            --packDir $publish --mainExe 'MordWare.App.exe' --icon (Join-Path $AppFolder $names.Icon) `
            --channel win --shortcuts StartMenuRoot --noPortable --delta None --outputDir $releases
    }
}
finally { Pop-Location }

$leftover = @(Get-ChildItem -LiteralPath $releases -Filter '*.zip' -File)
if ($leftover) { throw "vpk produced a portable zip ($($leftover.Name -join ', ')); it is never shipped." }

# 4. Package scan.
Write-Host '==> Package scan' -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'check-public-safety.ps1') -Package $releases, $publish
if ($LASTEXITCODE -ne 0) { throw 'The package scan failed: nothing from this pack may be published.' }

Write-Host "==> Done: $releases" -ForegroundColor Green
