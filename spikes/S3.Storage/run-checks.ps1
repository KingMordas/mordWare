<#
.SYNOPSIS
    Driver for the S3 MSIX storage spike (roadmap [S3], change spike-msix-storage, design D5).

.DESCRIPTION
    Installs, runs, updates, resets and uninstalls the spike probe package MordWare.Spike.S3 and checks
    from OUTSIDE the package (this plain PowerShell process) what really landed on disk. Each check
    prints PASS, FAIL, OBSERVED or UNVERIFIED with its evidence.

    Safety (guardrail 7): every package operation goes through Assert-SpikePackageName, which refuses
    any name other than exactly MordWare.Spike.S3, so MordWare (PROD) and MordWare.Dev (DEV) are never
    touched. All files live under "mordWare S3 spike" folders, deleted by -CleanupOnly.

    Requirements: Windows Developer Mode and the .NET SDK pinned in global.json. Run from any folder.

.PARAMETER Check
    Checks to run, in the given order: a, b, c, d, g, h, i (automated), f-backup and f-restore
    (OneDrive, with a manual step between them), e-verify (after the manual picker check).
    Default: b, a, d, g, h, i, c.

.PARAMETER Mode
    How the spike is installed (design D1).
    Dev: a development-mode registration by `dotnet run`, exactly as run-dev.ps1 deploys DEV.
    Msix: a real .msix (makeappx + signtool + Add-AppxPackage), as an installed PROD behaves.
    Windows refuses to reset a development-mode registration, so check d needs Msix. Windows also
    refuses unsigned packages with executable activations (0x80073D2B), so Msix needs -SigningThumbprint.

.PARAMETER SigningThumbprint
    Msix mode: thumbprint of a temporary self-signed "CN=mordWare" code-signing certificate in
    Cert:\CurrentUser\My, whose public part the owner trusted in Cert:\LocalMachine\TrustedPeople.

.PARAMETER CleanupOnly
    Uninstall every spike package (both modes) and delete every spike folder, then verify nothing remains.

.PARAMETER SelfTest
    Verify the package-name guard refuses MordWare, MordWare.Dev and near-misses.

.PARAMETER PickerFolder
    For e-verify: the folder picked in the probe window.
#>
[CmdletBinding()]
param(
    [string[]]$Check = @('b', 'a', 'd', 'g', 'h', 'i', 'c'),
    [ValidateSet('Dev', 'Msix')][string]$Mode = 'Dev',
    [string]$SigningThumbprint,
    [switch]$CleanupOnly,
    [switch]$SelfTest,
    [string]$PickerFolder
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$SpikePackageName = 'MordWare.Spike.S3'
$SpikeProject = Join-Path $PSScriptRoot 'S3.Storage.csproj'
$SpikeManifest = Join-Path $PSScriptRoot 'Package.appxmanifest'
$Alias = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\s3probe.exe'
$Documents = [Environment]::GetFolderPath('MyDocuments')
$SpikeRoot = Join-Path $Documents 'mordWare S3 spike'
$Results = Join-Path $SpikeRoot 'results'
$BackupsProd = Join-Path $SpikeRoot 'Backups'
$BackupsDev = Join-Path $SpikeRoot 'Backups (DEV)'
$TrapNew = Join-Path $env:LOCALAPPDATA 'mordWare S3 spike'
$TrapExisting = Join-Path $env:LOCALAPPDATA 'mordWare S3 spike existing'
$MsixStaging = Join-Path $env:TEMP 'mordWare S3 spike msix'
# Check e: a folder outside Documents and outside AppData, picked by the owner in the probe window.
$PickerTestFolder = Join-Path $env:USERPROFILE 'mordWare S3 spike picker'

$script:Summary = [System.Collections.Generic.List[object]]::new()

function Write-Step([string]$Message) { Write-Host "==> $Message" -ForegroundColor Cyan }

function Write-Check([string]$Name, [string]$Status, [string[]]$Evidence) {
    if ($Name -notin 'cleanup', 'self-test') { $Name = "$Name ($Mode)" }
    $color = switch ($Status) { 'PASS' { 'Green' } 'FAIL' { 'Red' } default { 'Yellow' } }
    Write-Host "[$Name] $Status" -ForegroundColor $color
    foreach ($line in $Evidence) { Write-Host "    $line" }
    $script:Summary.Add([pscustomobject]@{ Check = $Name; Status = $Status })
}

# --- Package helpers (guarded) ---------------------------------------------------------------------

function Assert-SpikePackageName([string]$Name) {
    if (-not ($Name -ceq $SpikePackageName)) {
        throw "Refusing to act on package '$Name': this driver only ever touches '$SpikePackageName'."
    }
}

function Get-SpikePackages {
    Assert-SpikePackageName $SpikePackageName
    @(Get-AppxPackage -Name $SpikePackageName | Where-Object { $_.Name -ceq $SpikePackageName })
}

# Only one install mode is present at a time (Install-Spike removes the other), so this is the one.
function Get-SpikePackage { Get-SpikePackages | Select-Object -First 1 }

function Get-SpikePackageRoot {
    # The family name is stable for this identity and publisher, so it is known after uninstall too.
    $family = (Get-SpikePackage).PackageFamilyName
    if (-not $family) { $family = $script:LastFamilyName }
    if (-not $family) { throw 'Spike package family name unknown; install the spike first.' }
    Join-Path $env:LOCALAPPDATA "Packages\$family"
}

function Install-Spike {
    Assert-SpikePackageName $SpikePackageName
    # Never leave the other mode installed: both would claim the s3probe.exe alias.
    foreach ($other in Get-SpikePackages | Where-Object { $_.IsDevelopmentMode -ne ($Mode -eq 'Dev') }) {
        Remove-SpikeRegistration $other
    }

    if ($Mode -eq 'Msix') { Install-SpikeMsix }
    else {
        Write-Step 'Building and registering the spike package (dotnet run, as run-dev.ps1 deploys DEV)'
        Push-Location $PSScriptRoot
        try {
            $output = & dotnet run --project $SpikeProject -c Debug -p:WinAppRunNoLaunch=true 2>&1
            if ($LASTEXITCODE -ne 0) { $output | Write-Host; throw "dotnet run failed with exit code $LASTEXITCODE." }
        }
        finally { Pop-Location }
    }

    $package = Get-SpikePackage
    if (-not $package) { throw "The spike package is not installed after the $Mode install." }
    $script:LastFamilyName = $package.PackageFamilyName
    Write-Step "Installed $($package.PackageFullName) (development mode: $($package.IsDevelopmentMode))"
}

# Msix mode (design D1): pack the Debug layout into a real .msix, sign it and install it.
function Install-SpikeMsix {
    if (-not $SigningThumbprint) { throw 'Msix mode needs -SigningThumbprint.' }
    Write-Step 'Building the spike (Debug) and packing a signed .msix'
    $output = & dotnet build $SpikeProject -c Debug 2>&1
    if ($LASTEXITCODE -ne 0) { $output | Write-Host; throw "dotnet build failed with exit code $LASTEXITCODE." }

    $layout = Get-ChildItem (Join-Path $PSScriptRoot 'bin\Debug') -Recurse -Directory -Filter 'AppX' | Select-Object -First 1
    if (-not $layout) { throw 'No AppX layout found under bin\Debug.' }
    $sdkTools = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.sdk.buildtools'
    $makeappx = Get-ChildItem $sdkTools -Recurse -Filter 'makeappx.exe' | Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1
    $signtool = Get-ChildItem $sdkTools -Recurse -Filter 'signtool.exe' | Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $makeappx -or -not $signtool) { throw 'makeappx.exe or signtool.exe not found in the Microsoft.Windows.SDK.BuildTools package.' }

    $cert = Get-Item "Cert:\CurrentUser\My\$SigningThumbprint" -ErrorAction SilentlyContinue
    if (-not $cert -or $cert.Subject -ne 'CN=mordWare') { throw "Signing certificate $SigningThumbprint (CN=mordWare) not found in CurrentUser\My." }

    Remove-Item $MsixStaging -Recurse -Force -ErrorAction SilentlyContinue
    $stagedLayout = Join-Path $MsixStaging 'layout'
    Copy-Item $layout.FullName $stagedLayout -Recurse
    $stagedManifest = Join-Path $stagedLayout 'AppxManifest.xml'
    $manifest = [IO.File]::ReadAllText($stagedManifest)
    if ($manifest -notmatch "Name=`"$([regex]::Escape($SpikePackageName))`" Publisher=`"CN=mordWare`"") {
        throw 'The staged manifest does not have the expected spike identity.'
    }
    # `dotnet build` does not refresh the AppX layout (only the dotnet run deploy does), so take the
    # version from the source manifest: check c bumps it there to test a real package update.
    $sourceVersion = [regex]::Match([IO.File]::ReadAllText($SpikeManifest), '<Identity[^>]*\sVersion="([^"]+)"').Groups[1].Value
    $manifest = [regex]::Replace($manifest, '(<Identity[^>]*\sVersion=")[^"]+(")', "`${1}$sourceVersion`${2}")
    [IO.File]::WriteAllText($stagedManifest, $manifest)

    $msix = Join-Path $MsixStaging "$SpikePackageName.msix"
    $packOutput = & $makeappx.FullName pack /d $stagedLayout /p $msix /o 2>&1
    if ($LASTEXITCODE -ne 0) { $packOutput | Write-Host; throw "makeappx failed with exit code $LASTEXITCODE." }
    $signOutput = & $signtool.FullName sign /fd SHA256 /sha1 $SigningThumbprint /s My $msix 2>&1
    if ($LASTEXITCODE -ne 0) { $signOutput | Write-Host; throw "signtool failed with exit code $LASTEXITCODE." }

    Write-Step "Installing $msix (signed by the temporary spike certificate)"
    Add-AppxPackage -Path $msix
}

function Remove-SpikeRegistration($Package) {
    Assert-SpikePackageName $Package.Name
    $script:LastFamilyName = $Package.PackageFamilyName
    Get-Process -Name 'S3.Storage' -ErrorAction SilentlyContinue | Stop-Process -Force
    Write-Step "Uninstalling $($Package.PackageFullName)"
    Remove-AppxPackage -Package $Package.PackageFullName
}

function Uninstall-Spike {
    foreach ($package in Get-SpikePackages) { Remove-SpikeRegistration $package }
}

function Reset-Spike {
    Assert-SpikePackageName $SpikePackageName
    $package = Get-SpikePackage
    Write-Step "Resetting $($package.PackageFullName)"
    Reset-AppxPackage -Package $package.PackageFullName
}

function Confirm-SpikeInstalled {
    $package = Get-SpikePackage
    if (-not $package -or $package.IsDevelopmentMode -ne ($Mode -eq 'Dev')) { Install-Spike }
    else { $script:LastFamilyName = $package.PackageFamilyName }
}

# --- Probe ------------------------------------------------------------------------------------------

function Invoke-Probe {
    param([Parameter(Mandatory)][string[]]$Arguments, [Parameter(Mandatory)][string]$Tag)
    $resultFile = Join-Path $Results "$Tag.json"
    Remove-Item $resultFile -ErrorAction SilentlyContinue
    $quoted = (@($Arguments) + @('--tag', $Tag) | ForEach-Object { '"' + $_ + '"' }) -join ' '
    $process = Start-Process -FilePath $Alias -ArgumentList $quoted -Wait -PassThru
    $deadline = (Get-Date).AddSeconds(15)
    while (-not (Test-Path $resultFile) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 200 }
    if (-not (Test-Path $resultFile)) { throw "Probe '$($Arguments -join ' ')' wrote no result (exit $($process.ExitCode))." }
    $result = Get-Content $resultFile -Raw | ConvertFrom-Json
    $result | Add-Member -NotePropertyName exitCode -NotePropertyValue $process.ExitCode
    $result
}

# A probe result has 'error' only when it failed; strict mode throws on a missing property.
function Get-ProbeError($Result) { if ($Result.PSObject.Properties['error']) { $Result.error } else { '' } }

function Get-LocalState { (Invoke-Probe -Arguments 'where' -Tag 'where').localFolder }

# --- Checks -----------------------------------------------------------------------------------------

function Test-CheckB {
    Confirm-SpikeInstalled
    foreach ($f in $TrapNew, $TrapExisting) { Remove-Item $f -Recurse -Force -ErrorAction SilentlyContinue }
    New-Item -ItemType Directory -Force $TrapExisting | Out-Null
    Set-Content (Join-Path $TrapExisting 'outside.txt') 'created outside the package'

    $root = Get-SpikePackageRoot
    $cases = @(
        @{ Name = 'new folder'; Real = (Join-Path $TrapNew 'probe.txt'); Virtual = (Join-Path $root 'LocalCache\Local\mordWare S3 spike\probe.txt') },
        @{ Name = 'existing folder'; Real = (Join-Path $TrapExisting 'probe.txt'); Virtual = (Join-Path $root 'LocalCache\Local\mordWare S3 spike existing\probe.txt') }
    )
    $evidence = @()
    $newRedirected = $false; $newGoneAfterUninstall = $false
    foreach ($case in $cases) {
        $write = Invoke-Probe -Arguments @('write', $case.Real, "b $($case.Name)") -Tag "b-write-$($case.Name -replace ' ', '-')"
        $realBefore = Test-Path $case.Real
        $virtualBefore = Test-Path $case.Virtual
        $evidence += "$($case.Name): probe wrote '$($case.Real)' ok=$($write.ok), probe reads it back=$($write.exists)"
        $evidence += "  seen from outside: real path=$realBefore, under package LocalCache=$virtualBefore ($($case.Virtual))"
        $case.RealBefore = $realBefore; $case.VirtualBefore = $virtualBefore
        if ($case.Name -eq 'new folder') { $newRedirected = (-not $realBefore) -and $virtualBefore }
    }

    Uninstall-Spike
    foreach ($case in $cases) {
        $realAfter = Test-Path $case.Real
        $virtualAfter = Test-Path $case.Virtual
        $evidence += "$($case.Name) after uninstall: real path=$realAfter, under package LocalCache=$virtualAfter"
        if ($case.Name -eq 'new folder') { $newGoneAfterUninstall = (-not $realAfter) -and (-not $virtualAfter) }
    }
    $evidence += "package folder after uninstall exists: $(Test-Path $root)"

    $status = if ($newRedirected -and $newGoneAfterUninstall) { 'PASS' } else { 'FAIL' }
    Write-Check 'b' $status (@('expected: a new %LOCALAPPDATA% folder is redirected into the package and deleted on uninstall') + $evidence)
}

function Test-CheckA {
    Confirm-SpikeInstalled
    $marker = Join-Path $BackupsProd 'a-marker.txt'
    $write = Invoke-Probe -Arguments @('write', $marker, 'a') -Tag 'a-write'
    $db = Invoke-Probe -Arguments @('create-db', '--edition', 'PROD') -Tag 'a-create-db'
    $backup = Invoke-Probe -Arguments @('backup', '--edition', 'PROD') -Tag 'a-backup'
    Uninstall-Spike
    $markerSurvived = Test-Path $marker
    $backupSurvived = $backup.ok -and (Test-Path $backup.file)
    $status = if ($write.ok -and $db.ok -and $markerSurvived -and $backupSurvived) { 'PASS' } else { 'FAIL' }
    Write-Check 'a' $status @(
        "Documents resolves to $Documents",
        "marker $marker survived uninstall: $markerSurvived",
        "backup $($backup.file) survived uninstall: $backupSurvived"
    )
}

function Test-CheckD {
    Confirm-SpikeInstalled
    $localState = Get-LocalState
    $stateMarker = Join-Path $localState 'd-marker.txt'
    $backupMarker = Join-Path $BackupsProd 'd-marker.txt'
    Invoke-Probe -Arguments @('write', $stateMarker, 'd') -Tag 'd-write-state' | Out-Null
    Invoke-Probe -Arguments @('write', $backupMarker, 'd') -Tag 'd-write-backup' | Out-Null
    $before = Test-Path $stateMarker
    Reset-Spike
    # Right after Reset-AppxPackage returns, the package can be briefly absent while Windows re-registers it.
    $deadline = (Get-Date).AddSeconds(15)
    while (-not (Get-SpikePackage) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
    $stillInstalled = [bool](Get-SpikePackage)
    $stateAfter = Test-Path $stateMarker
    $backupAfter = Test-Path $backupMarker
    $status = if ($before -and $stillInstalled -and -not $stateAfter -and $backupAfter) { 'PASS' } else { 'FAIL' }
    Write-Check 'd' $status @(
        "LocalState marker before reset: $before, after reset: $stateAfter",
        "backup-folder marker after reset: $backupAfter",
        "package still installed after reset: $stillInstalled"
    )
}

function Test-CheckC {
    Confirm-SpikeInstalled
    $localState = Get-LocalState
    $marker = Join-Path $localState 'c-marker.txt'
    Invoke-Probe -Arguments @('write', $marker, 'c') -Tag 'c-write' | Out-Null
    $evidence = @("marker written: $(Test-Path $marker)")

    # Scenario 1: install again at the same version (Dev: exactly as run-dev.ps1 deploys DEV).
    Install-Spike
    $same = Invoke-Probe -Arguments @('read', $marker) -Tag 'c-read-same'
    $evidence += "same-version re-install ($Mode): marker seen outside=$(Test-Path $marker), by probe=$($same.exists), version=$((Get-SpikePackage).Version)"

    # Scenario 2: bump the manifest version, rebuild and re-register. The manifest is always restored.
    $original = [IO.File]::ReadAllText($SpikeManifest)
    try {
        [IO.File]::WriteAllText($SpikeManifest, $original.Replace('Version="0.1.0.0"', 'Version="0.1.1.0"'))
        Install-Spike
        $bumped = Invoke-Probe -Arguments @('read', $marker) -Tag 'c-read-bumped'
        $evidence += "version update to $((Get-SpikePackage).Version): marker seen outside=$(Test-Path $marker), by probe=$($bumped.exists)"
    }
    finally {
        [IO.File]::WriteAllText($SpikeManifest, $original)
    }

    # Leave no bumped registration behind: the next install starts again at 0.1.0.0.
    Uninstall-Spike
    $status = if ($same.exists -and $bumped.exists) { 'PASS' } else { 'FAIL' }
    Write-Check 'c' $status $evidence
}

function Test-CheckG {
    Confirm-SpikeInstalled
    $folder = Join-Path $SpikeRoot 'g'
    Remove-Item $folder -Recurse -Force -ErrorAction SilentlyContinue
    $db = Invoke-Probe -Arguments @('create-db', '--edition', 'PROD') -Tag 'g-create-db'
    $first = Invoke-Probe -Arguments @('backup', $folder, '--edition', 'PROD') -Tag 'g-first'
    $interrupted = Invoke-Probe -Arguments @('backup', $folder, '--edition', 'PROD', '--interrupt') -Tag 'g-interrupt'
    $afterInterrupt = @(Get-ChildItem $folder -File | Select-Object -ExpandProperty Name)
    $completeAfterInterrupt = @($afterInterrupt | Where-Object { $_ -like '*.s3bak' })
    $restore = Invoke-Probe -Arguments @('restore', $folder, $db.recoveryKey, '--edition', 'PROD') -Tag 'g-restore'
    $next = Invoke-Probe -Arguments @('backup', $folder, '--edition', 'PROD') -Tag 'g-next'
    $afterNext = @(Get-ChildItem $folder -File | Select-Object -ExpandProperty Name)

    $ok = $interrupted.exitCode -eq 3 -and $interrupted.interrupted -and
          $completeAfterInterrupt.Count -eq 1 -and
          $restore.ok -and ($restore.file -eq $first.file) -and $restore.restoredRows -eq 1000 -and
          @($next.removedLeftovers).Count -eq 2 -and @($afterNext | Where-Object { $_ -like '*.tmp' }).Count -eq 0
    Write-Check 'g' $(if ($ok) { 'PASS' } else { 'FAIL' }) @(
        "interrupted backup: exit $($interrupted.exitCode), left behind $(@($interrupted.leftBehind) -join ', ')",
        "folder after interrupt: $($afterInterrupt -join ', ')",
        "restore from folder picked $([IO.Path]::GetFileName($restore.file)) (first complete: $([IO.Path]::GetFileName($first.file))), rows $($restore.restoredRows), ok=$($restore.ok)",
        "next backup removed: $(@($next.removedLeftovers) -join ', ')",
        "folder after next backup: $($afterNext -join ', ')"
    )
}

function Test-CheckH {
    Confirm-SpikeInstalled
    $folder = Join-Path $SpikeRoot 'h'
    Remove-Item $folder -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force $folder | Out-Null

    $prodDb = Invoke-Probe -Arguments @('create-db', '--edition', 'PROD') -Tag 'h-create-prod'
    $prodBak = Invoke-Probe -Arguments @('backup', '--edition', 'PROD') -Tag 'h-backup-prod'
    $devDb = Invoke-Probe -Arguments @('create-db', '--edition', 'DEV') -Tag 'h-create-dev'
    $devBak = Invoke-Probe -Arguments @('backup', '--edition', 'DEV') -Tag 'h-backup-dev'
    $foldersOk = ((Split-Path $prodBak.file) -eq $BackupsProd) -and ((Split-Path $devBak.file) -eq $BackupsDev)

    $devToProd = Invoke-Probe -Arguments @('restore', $devBak.file, $devDb.recoveryKey, '--edition', 'PROD') -Tag 'h-dev-to-prod'
    $prodToDev = Invoke-Probe -Arguments @('restore', $prodBak.file, $prodDb.recoveryKey, '--edition', 'DEV') -Tag 'h-prod-to-dev'
    $openDev = Invoke-Probe -Arguments @('open-db', '--edition', 'DEV') -Tag 'h-open-dev'

    $liveDb = Join-Path (Get-LocalState) 'Data\spike.db'
    $liveHashBefore = (Get-FileHash $liveDb).Hash

    $flipped = Join-Path $folder 'flipped.s3bak'
    $bytes = [IO.File]::ReadAllBytes($devBak.file)
    $bytes[6] = 0  # edition byte: Dev (1) -> Prod (0)
    [IO.File]::WriteAllBytes($flipped, $bytes)
    $flip = Invoke-Probe -Arguments @('restore', $flipped, $devDb.recoveryKey, '--edition', 'DEV') -Tag 'h-flipped'

    $truncated = Join-Path $folder 'truncated.s3bak'
    $bytes = [IO.File]::ReadAllBytes($prodBak.file)
    [IO.File]::WriteAllBytes($truncated, $bytes[0..($bytes.Length - 1001)])
    $trunc = Invoke-Probe -Arguments @('restore', $truncated, $prodDb.recoveryKey, '--edition', 'DEV') -Tag 'h-truncated'

    $liveHashAfter = (Get-FileHash $liveDb).Hash
    $openAfter = Invoke-Probe -Arguments @('open-db', '--edition', 'DEV') -Tag 'h-open-after'

    $ok = $foldersOk -and
          (-not $devToProd.ok) -and (Get-ProbeError $devToProd) -like '*DEV backup cannot be restored into PROD*' -and
          $prodToDev.ok -and $prodToDev.rewrappedAs -eq 'Dev' -and $openDev.rows -eq 1000 -and
          (-not $flip.ok) -and (Get-ProbeError $flip) -like '*System.Security.Cryptography.*' -and
          (-not $trunc.ok) -and (Get-ProbeError $trunc) -like '*hash mismatch*' -and
          $liveHashBefore -eq $liveHashAfter -and $openAfter.rows -eq 1000
    Write-Check 'h' $(if ($ok) { 'PASS' } else { 'FAIL' }) @(
        "PROD backup in $(Split-Path $prodBak.file); DEV backup in $(Split-Path $devBak.file)",
        "DEV -> PROD: ok=$($devToProd.ok) $(Get-ProbeError $devToProd)",
        "PROD -> DEV: ok=$($prodToDev.ok), rewrapped as $($prodToDev.rewrappedAs); opens as DEV with $($openDev.rows) rows",
        "flipped edition byte: ok=$($flip.ok) $(Get-ProbeError $flip)",
        "truncated by 1000 bytes: ok=$($trunc.ok) $(Get-ProbeError $trunc)",
        "live database unchanged by the refusals: $($liveHashBefore -eq $liveHashAfter), rows $($openAfter.rows)"
    )
}

function Test-CheckI {
    Confirm-SpikeInstalled
    $db = Invoke-Probe -Arguments @('create-db', '--edition', 'PROD') -Tag 'i-create-db'
    $backup = Invoke-Probe -Arguments @('backup', '--edition', 'PROD') -Tag 'i-backup'
    $root = Get-SpikePackageRoot
    Uninstall-Spike
    $localStateGone = -not (Test-Path (Join-Path $root 'LocalState'))
    $backupKept = Test-Path $backup.file

    Install-Spike
    $openBefore = Invoke-Probe -Arguments @('open-db', '--edition', 'PROD') -Tag 'i-open-before-restore'
    $restore = Invoke-Probe -Arguments @('restore', $backup.file, $db.recoveryKey, '--edition', 'PROD') -Tag 'i-restore'
    $openAfter = Invoke-Probe -Arguments @('open-db', '--edition', 'PROD') -Tag 'i-open-after-restore'

    $ok = $db.ok -and $backup.ok -and $localStateGone -and $backupKept -and (-not $openBefore.ok) -and
          $restore.ok -and $restore.restoredRows -eq 1000 -and $openAfter.ok -and $openAfter.rows -eq 1000 -and $openAfter.wrongKeyRefused
    Write-Check 'i' $(if ($ok) { 'PASS' } else { 'FAIL' }) @(
        "created $($db.rows) rows; backup $([IO.Path]::GetFileName($backup.file))",
        "after uninstall: LocalState gone=$localStateGone, backup kept=$backupKept",
        "after reinstall, before restore: open-db ok=$($openBefore.ok) $(Get-ProbeError $openBefore)",
        "restore with backup file + recovery key only: ok=$($restore.ok), rows $($restore.restoredRows), quick_check $($restore.quickCheck)",
        "next probe run opens from the re-created DPAPI key file: ok=$($openAfter.ok), rows $($openAfter.rows), wrong key refused=$($openAfter.wrongKeyRefused)"
    )
}

function Test-CheckFBackup {
    Confirm-SpikeInstalled
    $where = Invoke-Probe -Arguments 'where' -Tag 'f-where'
    if (-not $where.documentsUnderOneDrive) {
        Write-Check 'f' 'UNVERIFIED' @("Documents ($($where.documents)) is not under a OneDrive root on this PC.")
        return
    }
    $folder = Join-Path $SpikeRoot 'f'
    Remove-Item $folder -Recurse -Force -ErrorAction SilentlyContinue
    $db = Invoke-Probe -Arguments @('create-db', '--edition', 'PROD') -Tag 'f-create-db'
    $backup = Invoke-Probe -Arguments @('backup', $folder, '--edition', 'PROD') -Tag 'f-backup'
    Set-Content (Join-Path $Results 'f-recovery-key.txt') $db.recoveryKey
    Write-Check 'f-backup' $(if ($backup.ok) { 'PASS' } else { 'FAIL' }) @(
        "backup written to $($backup.file) ($($backup.payloadBytes) payload bytes)",
        'NEXT (manual): wait until OneDrive shows it synced, right-click it > Free up space, then run -Check f-restore'
    )
}

function Test-CheckFRestore {
    Confirm-SpikeInstalled
    $folder = Join-Path $SpikeRoot 'f'
    $file = Get-ChildItem $folder -Filter '*.s3bak' | Sort-Object Name -Descending | Select-Object -First 1
    $recallOnDataAccess = 0x400000
    $attributesBefore = [int](Get-Item $file.FullName -Force).Attributes
    $cloudOnly = ($attributesBefore -band $recallOnDataAccess) -ne 0
    $key = (Get-Content (Join-Path $Results 'f-recovery-key.txt') -Raw).Trim()
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $restore = Invoke-Probe -Arguments @('restore', $file.FullName, $key, '--edition', 'PROD') -Tag 'f-restore'
    $elapsed = $sw.ElapsedMilliseconds
    $attributesAfter = [int](Get-Item $file.FullName -Force).Attributes
    $status = if (-not $cloudOnly) { 'UNVERIFIED' } elseif ($restore.ok -and $restore.restoredRows -eq 1000) { 'PASS' } else { 'FAIL' }
    Write-Check 'f' $status @(
        "file $($file.Name): cloud-only before restore=$cloudOnly (attributes 0x$('{0:X}' -f $attributesBefore))",
        "restore: ok=$($restore.ok), rows $($restore.restoredRows), $elapsed ms including download $(Get-ProbeError $restore)",
        "attributes after restore: 0x$('{0:X}' -f $attributesAfter)"
    )
}

function Test-CheckEVerify {
    if (-not $PickerFolder) { $PickerFolder = $PickerTestFolder }
    $files = @(Get-ChildItem $PickerFolder -Filter 's3-picker-*.txt' -ErrorAction SilentlyContinue)
    Write-Check 'e' $(if ($files.Count -ge 2) { 'PASS' } else { 'FAIL' }) @(
        "picker test files in ${PickerFolder}: $($files.Count) ($(($files | Select-Object -ExpandProperty Name) -join ', '))"
    )
}

function Invoke-Cleanup {
    Uninstall-Spike
    foreach ($folder in $SpikeRoot, $TrapNew, $TrapExisting, $MsixStaging, $PickerTestFolder) {
        if (Test-Path $folder) { Write-Step "Deleting $folder"; Remove-Item $folder -Recurse -Force }
    }
    $leftPackages = @(Get-ChildItem (Join-Path $env:LOCALAPPDATA 'Packages') -Directory -Filter "$SpikePackageName*" -ErrorAction SilentlyContinue)
    $leftFolders = @($SpikeRoot, $TrapNew, $TrapExisting, $MsixStaging, $PickerTestFolder | Where-Object { Test-Path $_ })
    $registered = @(Get-SpikePackages).Count -gt 0
    $clean = -not $registered -and $leftPackages.Count -eq 0 -and $leftFolders.Count -eq 0
    Write-Check 'cleanup' $(if ($clean) { 'PASS' } else { 'FAIL' }) @(
        "spike package registered: $registered",
        "package folders left: $(($leftPackages | Select-Object -ExpandProperty Name) -join ', ')",
        "spike folders left: $($leftFolders -join ', ')",
        "mordWare packages now: $((Get-AppxPackage | Where-Object { $_.Name -like 'MordWare*' } | Select-Object -ExpandProperty PackageFullName) -join ', ')"
    )
}

function Invoke-SelfTest {
    $refused = @()
    foreach ($name in 'MordWare', 'MordWare.Dev', 'mordware.spike.s3', 'MordWare.Spike.S3x', '') {
        try { Assert-SpikePackageName $name; $refused += "NOT refused: '$name'" }
        catch { $refused += "refused: '$name'" }
    }
    $accepted = try { Assert-SpikePackageName $SpikePackageName; $true } catch { $false }
    $ok = $accepted -and -not ($refused -like 'NOT*')
    Write-Check 'self-test' $(if ($ok) { 'PASS' } else { 'FAIL' }) ($refused + "accepted '$SpikePackageName': $accepted")
}

# --- Main -------------------------------------------------------------------------------------------

$devModeKey = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock'
$devMode = (Get-ItemProperty -Path $devModeKey -Name AllowDevelopmentWithoutDevLicense -ErrorAction SilentlyContinue).AllowDevelopmentWithoutDevLicense
if ($devMode -ne 1) { throw 'Windows Developer Mode is off. Turn it on in Settings > System > For developers.' }

if ($SelfTest) { Invoke-SelfTest }
elseif ($CleanupOnly) { Invoke-Cleanup }
else {
    foreach ($name in $Check) {
        switch ($name) {
            'a' { Test-CheckA }
            'b' { Test-CheckB }
            'c' { Test-CheckC }
            'd' { Test-CheckD }
            'g' { Test-CheckG }
            'h' { Test-CheckH }
            'i' { Test-CheckI }
            'f-backup' { Test-CheckFBackup }
            'f-restore' { Test-CheckFRestore }
            'e-verify' { Test-CheckEVerify }
            default { throw "Unknown check '$name'." }
        }
    }
}

Write-Host ''
$script:Summary | Format-Table -AutoSize | Out-String | Write-Host
