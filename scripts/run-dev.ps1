<#
.SYNOPSIS
    Builds and launches the DEV edition of mordWare ("mordWare (DEV)", package MordWare.Dev).

.DESCRIPTION
    Builds the app in Debug, which always packages the DEV identity, registers it from the build
    output and starts it. The script never builds Release and never touches the PROD package
    (MordWare) or its data: DEV and PROD never mix (CLAUDE.md guardrail 7).

    Requirements: Windows Developer Mode, and the .NET SDK pinned in global.json.
    Works in Windows PowerShell 5.1 and PowerShell 7+.

.PARAMETER NoBuild
    Skip the build and start the DEV edition that is already deployed.

.PARAMETER Reset
    Remove the DEV registration first (fixes a stale or broken deploy). This DELETES the DEV
    edition's data folder; PROD is never affected. Asks for confirmation unless -Force is given.

.PARAMETER Force
    With -Reset, skip the confirmation prompt.

.EXAMPLE
    .\scripts\run-dev.ps1
    Build and launch the DEV edition.

.EXAMPLE
    .\scripts\run-dev.ps1 -NoBuild
    Launch the already-deployed DEV edition without rebuilding.

.EXAMPLE
    .\scripts\run-dev.ps1 -Reset
    Remove the DEV registration and its data, then build and launch from scratch.
#>
[CmdletBinding()]
param(
    [switch]$NoBuild,
    [switch]$Reset,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$DevPackageName = 'MordWare.Dev'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$AppProject = Join-Path $RepoRoot 'src\MordWare.App\MordWare.App.csproj'

function Write-Step([string]$Message) { Write-Host "==> $Message" -ForegroundColor Cyan }

function Get-DevPackage {
    # Exact name match: never returns the PROD package (MordWare).
    Get-AppxPackage -Name $DevPackageName | Where-Object { $_.Name -ceq $DevPackageName }
}

function Start-DevPackage {
    $package = Get-DevPackage
    if (-not $package) {
        throw "The DEV edition is not deployed yet. Run this script without -NoBuild."
    }
    $aumid = "$($package.PackageFamilyName)!App"
    Write-Step "Starting mordWare (DEV) [$aumid]"
    Start-Process "shell:AppsFolder\$aumid"
}

if ($NoBuild -and $Reset) {
    throw '-NoBuild and -Reset cannot be combined: a reset removes the deployed DEV edition, so it must be rebuilt.'
}

# --- Prerequisites --------------------------------------------------------------------------------
$devModeKey = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock'
$devMode = (Get-ItemProperty -Path $devModeKey -Name AllowDevelopmentWithoutDevLicense -ErrorAction SilentlyContinue).AllowDevelopmentWithoutDevLicense
if ($devMode -ne 1) {
    throw 'Windows Developer Mode is off. Turn it on in Settings > System > For developers, then run this script again.'
}

if (-not $NoBuild) {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'The .NET SDK (dotnet) was not found. Install the version pinned in global.json.'
    }
    if (-not (Test-Path $AppProject)) {
        throw "App project not found at $AppProject."
    }
}

# --- Optional reset (DEV only) --------------------------------------------------------------------
if ($Reset) {
    $existing = Get-DevPackage
    if ($existing) {
        if (-not $Force) {
            $answer = Read-Host "This removes the DEV edition and DELETES its data (PROD is not touched). Type 'yes' to continue"
            if ($answer -ne 'yes') {
                Write-Host 'Reset cancelled.'
                return
            }
        }
        Get-Process -Name 'MordWare.App' -ErrorAction SilentlyContinue |
            Where-Object { $_.Path -and $_.Path -like "$($existing.InstallLocation)*" } |
            Stop-Process
        Write-Step "Removing $($existing.PackageFullName)"
        Remove-AppxPackage -Package $existing.PackageFullName
    }
    else {
        Write-Step 'No DEV registration to remove.'
    }
}

# --- Build, register, launch ----------------------------------------------------------------------
if ($NoBuild) {
    Start-DevPackage
    return
}

# Debug always packages the DEV identity (Package.Dev.appxmanifest); Release is never used here.
# `dotnet run` builds and registers the package from the build output. WinAppRunNoLaunch stops it
# from launching the app itself: that launch keeps `dotnet run` waiting until the window closes.
# The app is then started through its Start menu identity, independent of this console, exactly
# as -NoBuild does.
Write-Step 'Building and deploying the DEV edition (Debug)'
Push-Location $RepoRoot
try {
    & dotnet run --project $AppProject -c Debug -p:WinAppRunNoLaunch=true
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet run failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

$package = Get-DevPackage
if (-not $package) {
    throw "Build finished but the $DevPackageName package is not registered."
}

Start-DevPackage
Write-Step "Data folder: $env:LOCALAPPDATA\Packages\$($package.PackageFamilyName)\LocalState"
