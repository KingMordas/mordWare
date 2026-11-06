<#
.SYNOPSIS
    Builds and launches the DEV edition of mordWare ("mordWare (DEV)") from its build output.

.DESCRIPTION
    Builds the app in Debug for x64, which is always the DEV edition (the edition is fixed by the
    build: only Release is PROD), and starts MordWare.App.exe from the build output, independent of
    this console. The DEV edition keeps its data in %LOCALAPPDATA%\mordWare Data (DEV)\.

    The script never builds Release, and never touches the PROD edition's data (mordWare Data) or
    the installer's folder %LOCALAPPDATA%\MordWare\: DEV and PROD never mix (CLAUDE.md guardrail 7).

    If a MordWare.Dev registration from the earlier MSIX setup is still on this account, the script
    offers once to remove it (DEV only, exact name match).

    Requirements: the .NET SDK pinned in global.json. Works in Windows PowerShell 5.1 and PowerShell 7+.

.PARAMETER NoBuild
    Skip the build and start the existing DEV build output.

.PARAMETER Reset
    Delete the DEV edition's data folder (mordWare Data (DEV)) first. PROD is never affected.
    Asks for confirmation unless -Force is given.

.PARAMETER Force
    With -Reset, skip the confirmation prompt.

.EXAMPLE
    .\scripts\run-dev.ps1
    Build and launch the DEV edition.

.EXAMPLE
    .\scripts\run-dev.ps1 -NoBuild
    Launch the existing DEV build without rebuilding.

.EXAMPLE
    .\scripts\run-dev.ps1 -Reset
    Delete the DEV data folder, then build and launch.
#>
[CmdletBinding()]
param(
    [switch]$NoBuild,
    [switch]$Reset,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The DEV names, as in MordWare.Core.Editions.EditionProfile.Dev. Guarded below: this script only ever
# handles a folder whose name ends in "(DEV)".
$DevDataFolderName = 'mordWare Data (DEV)'
$LegacyMsixDevPackage = 'MordWare.Dev'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$AppProject = Join-Path $RepoRoot 'src\MordWare.App\MordWare.App.csproj'
$DebugOutput = Join-Path $RepoRoot 'src\MordWare.App\bin\x64\Debug'

function Write-Step([string]$Message) { Write-Host "==> $Message" -ForegroundColor Cyan }

function Get-DevDataFolder {
    if ($DevDataFolderName -notmatch '\(DEV\)$') {
        throw "Refusing '$DevDataFolderName': the DEV data folder name must end in '(DEV)'."
    }
    $localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    if (-not $localAppData) {
        throw 'The local application data folder is not available.'
    }
    Join-Path $localAppData $DevDataFolderName
}

function Find-DevExe {
    if (-not (Test-Path $DebugOutput)) { return $null }
    Get-ChildItem -Path $DebugOutput -Recurse -Filter 'MordWare.App.exe' -File |
        Where-Object { $_.FullName -notmatch '\\publish\\' } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
}

function Stop-DevBuildProcesses {
    # Only processes started from this repository's Debug build output, never an installed edition.
    Get-Process -Name 'MordWare.App' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($DebugOutput, [StringComparison]::OrdinalIgnoreCase) } |
        Stop-Process
}

function Invoke-LegacyMsixOffer {
    # The MSIX era registered DEV as a package. Offer once to remove a leftover registration; the
    # answer "no" is remembered in the DEV data folder, so the question is not repeated.
    $declinedMarker = Join-Path (Get-DevDataFolder) '.run-dev-msix-removal-declined'
    if (Test-Path $declinedMarker) { return }

    $package = $null
    try {
        $package = Get-AppxPackage -Name $LegacyMsixDevPackage -ErrorAction Stop |
            Where-Object { $_.Name -ceq $LegacyMsixDevPackage }
    }
    catch {
        return  # Appx cmdlets unavailable in this shell: nothing to offer.
    }
    if (-not $package) { return }

    Write-Host "A leftover '$LegacyMsixDevPackage' app registration from the earlier MSIX setup is on this account."
    Write-Host 'It is no longer used. Removing it deletes only that registration and its old test data (PROD is not touched).'
    $answer = Read-Host "Type 'yes' to remove it now (asked only once)"
    if ($answer -eq 'yes') {
        foreach ($p in @($package)) {
            Write-Step "Removing $($p.PackageFullName)"
            Remove-AppxPackage -Package $p.PackageFullName
        }
    }
    else {
        New-Item -ItemType Directory -Force -Path (Split-Path $declinedMarker) | Out-Null
        Set-Content -Path $declinedMarker -Value 'run-dev.ps1: leftover MSIX DEV registration kept by choice.'
        Write-Host 'Kept. To remove it later: Get-AppxPackage MordWare.Dev | Remove-AppxPackage'
    }
}

if ($NoBuild -and $Reset) {
    throw '-NoBuild and -Reset cannot be combined.'
}

if (-not $NoBuild) {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'The .NET SDK (dotnet) was not found. Install the version pinned in global.json.'
    }
    if (-not (Test-Path $AppProject)) {
        throw "App project not found at $AppProject."
    }
}

# --- Optional reset (DEV data only) ---------------------------------------------------------------
if ($Reset) {
    $dataFolder = Get-DevDataFolder
    if (Test-Path $dataFolder) {
        if (-not $Force) {
            $answer = Read-Host "This DELETES the DEV data folder '$DevDataFolderName' (PROD is not touched). Type 'yes' to continue"
            if ($answer -ne 'yes') {
                Write-Host 'Reset cancelled.'
                return
            }
        }
        Stop-DevBuildProcesses
        Write-Step "Deleting $DevDataFolderName"
        Remove-Item -LiteralPath $dataFolder -Recurse -Force
    }
    else {
        Write-Step 'No DEV data folder to delete.'
    }
}

Invoke-LegacyMsixOffer

# --- Build and launch -----------------------------------------------------------------------------
if (-not $NoBuild) {
    # Debug is always DEV; Release (PROD) is never built here.
    Write-Step 'Building the DEV edition (Debug, x64)'
    & dotnet build $AppProject -c Debug -p:Platform=x64 --nologo -v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }
}

$exe = Find-DevExe
if (-not $exe) {
    throw 'No DEV build found. Run this script without -NoBuild.'
}

Write-Step 'Starting mordWare (DEV)'
Start-Process -FilePath $exe.FullName -WorkingDirectory $exe.DirectoryName
Write-Step "Data folder: %LOCALAPPDATA%\$DevDataFolderName"
