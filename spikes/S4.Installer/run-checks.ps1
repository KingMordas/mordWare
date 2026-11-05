<#
.SYNOPSIS
    Driver of the S4 installer spike (roadmap [S4], change spike-unpackaged-installer, design D5).

.DESCRIPTION
    Packs the unpackaged probe app with Velopack (vpk), installs, upgrades, updates and uninstalls it
    per user, and checks the file system from outside. It only ever touches spike names:
      packIds      MordWare.Spike.S4 (Release, PROD-like) and MordWare.Spike.S4.Dev (Debug, DEV-like)
      data roots   %LOCALAPPDATA%\mordWare S4 spike Data and ...\mordWare S4 spike Data (DEV)
      feed         %TEMP%\mordWare S4 spike feed, publish output %TEMP%\mordWare S4 spike publish
    Every install, uninstall and delete helper refuses any other name (Assert-SpikeName), so the
    real editions (MordWare, MordWare.Dev) and the real data roots (mordWare Data*) are unreachable.
    It never changes a Windows setting, policy or optional feature, and never needs admin rights.

    Requires PowerShell 7+ and the .NET SDK; vpk comes from the local tool manifest (.config).

.EXAMPLE
    .\run-checks.ps1 -Check a
    .\run-checks.ps1 -CleanupOnly
#>
[CmdletBinding()]
param(
    [ValidateSet('a', 'b', 'c', 'd', 'g', 'h', 'j', 'sizes')][string]$Check,
    [switch]$CleanupOnly,
    [switch]$SelfTestGuard
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$Root = $PSScriptRoot
$Feed = Join-Path $env:TEMP 'mordWare S4 spike feed'
$PublishRoot = Join-Path $env:TEMP 'mordWare S4 spike publish'
$Editions = @{
    PROD = @{ PackId = 'MordWare.Spike.S4'; Configuration = 'Release'; Title = 'mordWare S4 spike'; DataRoot = Join-Path $env:LOCALAPPDATA 'mordWare S4 spike Data' }
    DEV  = @{ PackId = 'MordWare.Spike.S4.Dev'; Configuration = 'Debug'; Title = 'mordWare S4 spike (DEV)'; DataRoot = Join-Path $env:LOCALAPPDATA 'mordWare S4 spike Data (DEV)' }
}
$Channel = 's4-spike'

function Assert-SpikeName([string]$Value) {
    # The guard (design D5): only spike packIds and spike folders, never the real editions.
    if ($Value -notmatch 'Spike\.S4' -and $Value -notmatch 'S4 spike' -and $Value -notmatch 'spike\.s4') {
        throw "Refused: '$Value' is not a spike name."
    }
}

function Remove-SpikePath([string]$Path) {
    Assert-SpikeName $Path
    if (Test-Path -LiteralPath $Path) { Remove-Item -LiteralPath $Path -Recurse -Force }
}

function Get-InstallDir([string]$PackId) { Assert-SpikeName $PackId; Join-Path $env:LOCALAPPDATA $PackId }

function Get-UninstallEntry([string]$PackId) {
    Assert-SpikeName $PackId
    $key = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$PackId"
    if (Test-Path $key) { Get-ItemProperty $key } else { $null }
}

function Get-StartMenuShortcut([string]$Title) {
    $dir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
    Get-ChildItem $dir -Recurse -Filter '*.lnk' -ErrorAction SilentlyContinue | Where-Object { $_.BaseName -like "$Title*" }
}

function Invoke-Pack([string]$Edition, [string]$Version) {
    $e = $Editions[$Edition]; Assert-SpikeName $e.PackId
    $pub = Join-Path $PublishRoot "$Edition-$Version"
    $out = Join-Path $Feed $Edition
    Remove-SpikePath $pub
    dotnet publish (Join-Path $Root 'S4.Installer.csproj') -c $e.Configuration -r win-x64 "-p:Version=$Version" -o $pub | Out-Null
    if ($LASTEXITCODE) { throw "publish failed ($LASTEXITCODE)" }
    Push-Location $Root
    try {
        dotnet vpk pack --packId $e.PackId --packVersion $Version --packDir $pub --mainExe S4Probe.exe `
            --packTitle $e.Title --channel $Channel --shortcuts StartMenuRoot --outputDir $out | Out-Null
        if ($LASTEXITCODE) { throw "vpk pack failed ($LASTEXITCODE)" }
    } finally { Pop-Location }
    # Keep a copy of each version's Setup.exe: the feed's Setup.exe is always the newest one.
    $setups = New-Item -ItemType Directory -Force (Join-Path $out 'setups')
    Copy-Item (Join-Path $out "$($e.PackId)-$Channel-Setup.exe") (Join-Path $setups "$Version-Setup.exe") -Force
    Write-Host "packed $($e.PackId) $Version"
}

function Invoke-Install([string]$Edition, [string]$Version) {
    $e = $Editions[$Edition]; Assert-SpikeName $e.PackId
    $setup = Join-Path $Feed "$Edition\setups\$Version-Setup.exe"
    $log = Join-Path $Feed "$Edition\setup-$Version.log"
    $p = Start-Process $setup -ArgumentList '--silent', '--log', "`"$log`"" -Wait -PassThru
    if ($p.ExitCode) {
        # Finding (check b): Setup over an existing install renames the packId folder first; right
        # after the app has run, that rename can be denied for a few seconds. Retry once, visibly.
        Write-Host "Setup.exe exited $($p.ExitCode); retrying once in 15 s"
        Start-Sleep -Seconds 15
        $p = Start-Process $setup -ArgumentList '--silent', '--log', "`"$log.retry`"" -Wait -PassThru
        if ($p.ExitCode) { throw "Setup.exe exited $($p.ExitCode) again" }
    }
    Start-Sleep -Seconds 2   # the first-run start of the app is asynchronous
    Write-Host "installed $($e.PackId) $Version"
}

function Invoke-Uninstall([string]$Edition) {
    $e = $Editions[$Edition]; Assert-SpikeName $e.PackId
    $entry = Get-UninstallEntry $e.PackId
    if (-not $entry) { Write-Host "$($e.PackId) not installed"; return }
    $update = Join-Path (Get-InstallDir $e.PackId) 'Update.exe'
    $p = Start-Process $update -ArgumentList '--uninstall', '--silent' -Wait -PassThru
    Write-Host "uninstalled $($e.PackId) (exit $($p.ExitCode))"
    Start-Sleep -Seconds 2
}

function Invoke-Probe([string]$Edition, [string[]]$ProbeArgs, [switch]$FromBuild) {
    $e = $Editions[$Edition]
    $exe = if ($FromBuild) {
        Join-Path $Root "bin\x64\$($e.Configuration)\net10.0-windows10.0.26100.0\win-x64\S4Probe.exe"
    } else {
        Join-Path (Get-InstallDir $e.PackId) 'current\S4Probe.exe'
    }
    # Start-Process joins arguments with spaces: quote the ones that contain spaces (feed paths).
    $quoted = @($ProbeArgs | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } })
    $p = Start-Process $exe -ArgumentList $quoted -Wait -PassThru
    return $p.ExitCode
}

function Read-Result([string]$Edition, [string]$Name) {
    $f = Join-Path $Editions[$Edition].DataRoot "results\$Name.json"
    if (Test-Path $f) { Get-Content $f -Raw | ConvertFrom-Json } else { $null }
}

function Wait-Result([string]$Edition, [string]$Name, [datetime]$After, [int]$Seconds = 60) {
    $f = Join-Path $Editions[$Edition].DataRoot "results\$Name.json"
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        if ((Test-Path $f) -and (Get-Item $f).LastWriteTime -gt $After) { return Get-Content $f -Raw | ConvertFrom-Json }
        Start-Sleep -Milliseconds 500
    }
    return $null
}

function Report([string]$Name, [bool]$Pass, [string]$Evidence) {
    $verdict = if ($Pass) { 'PASS' } else { 'FAIL' }
    Write-Host ("[{0}] {1}: {2}" -f $verdict, $Name, $Evidence)
    if (-not $Pass) { $script:Failed = $true }
}

function Invoke-Cleanup {
    foreach ($ed in 'PROD', 'DEV') {
        Invoke-Uninstall $ed
        Remove-SpikePath (Get-InstallDir $Editions[$ed].PackId)
        Remove-SpikePath $Editions[$ed].DataRoot
        Remove-SpikePath (Join-Path $env:LOCALAPPDATA ($Editions[$ed].PackId.ToLowerInvariant()))
    }
    Remove-SpikePath $Feed
    Remove-SpikePath $PublishRoot
    Write-Host 'cleanup done'
}

$script:Failed = $false

if ($SelfTestGuard) {
    foreach ($bad in 'MordWare', 'MordWare.Dev', (Join-Path $env:LOCALAPPDATA 'mordWare Data'), (Join-Path $env:LOCALAPPDATA 'mordWare Data (DEV)')) {
        $refused = $false
        try { Remove-SpikePath $bad } catch { $refused = $_.Exception.Message -like 'Refused*' }
        try { $null = Get-InstallDir $bad; $refusedInstall = $false } catch { $refusedInstall = $true }
        Report "guard refuses '$bad'" ($refused -and $refusedInstall) 'Remove-SpikePath and Get-InstallDir both throw'
    }
    exit [int]$script:Failed
}

if ($CleanupOnly) { Invoke-Cleanup; exit 0 }

switch ($Check) {
    'd' {
        # Unpackaged runtime from the build output (DEV) and the Release build (PROD-like).
        foreach ($ed in 'DEV', 'PROD') {
            dotnet build (Join-Path $Root 'S4.Installer.csproj') -c $Editions[$ed].Configuration | Out-Null
            foreach ($tag in 'en-US', 'it-IT') {
                $code = Invoke-Probe $ed @('lang', $tag) -FromBuild
                $r = Read-Result $ed "lang-$tag"
                $expected = if ($tag -eq 'it-IT') { 'Ciao dallo spike' } else { 'Hello from the spike' }
                Report "d $ed lang $tag" ($code -eq 0 -and $r.greeting -eq $expected -and $r.overrideNow -eq $tag) `
                    "greeting='$($r.greeting)' marker='$($r.devMarker)' date='$($r.formattedDate)' number='$($r.formattedNumber)' override='$($r.overrideNow)' Windows.Globalization='$($r.windowsGlobalizationOverride)'"
            }
            $null = Invoke-Probe $ed @('where') -FromBuild
            $w = Read-Result $ed 'where'
            Report "d $ed edition" ($w.edition -eq $ed -and $w.expectedPackId -eq $Editions[$ed].PackId) "edition=$($w.edition) packId=$($w.expectedPackId) installed=$($w.isInstalled)"
            $null = Invoke-Probe $ed @('check-update', '--feed', (Join-Path $Feed $ed)) -FromBuild
            $c = Read-Result $ed 'check-update'
            Report "d $ed build output skips updates" ($c.isInstalled -eq $false) "skipped='$($c.skipped)'"
        }
    }
    'j' {
        # Velopack hook first, then WinUI, from the build output and from an install.
        $ed = 'PROD'
        dotnet build (Join-Path $Root 'S4.Installer.csproj') -c Release | Out-Null
        Invoke-Install PROD '0.1.0'
        foreach ($mode in 'build', 'installed') {
            $t = Get-Date
            $exe = if ($mode -eq 'build') { Join-Path $Root 'bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\S4Probe.exe' } else { Join-Path (Get-InstallDir $Editions[$ed].PackId) 'current\S4Probe.exe' }
            $p = Start-Process $exe -PassThru
            $win = Wait-Result $ed 'window' $t 20
            $p.Refresh()
            $entry = Read-Result $ed 'entry'
            $title = $p.MainWindowTitle
            Stop-Process -Id $p.Id -Force
            Report "j $mode" ($null -ne $win -and $entry.velopackHookRanBeforeUi -and $title -eq $Editions[$ed].Title) "window title='$title' entry.started=$($entry.started)"
        }
        $code = Invoke-Probe $ed @('lang', 'it-IT')
        $l = Read-Result $ed 'lang-it-IT'
        Report 'j installed lang it-IT' ($code -eq 0 -and $l.greeting -eq 'Ciao dallo spike') "greeting='$($l.greeting)' resourceFile=...\$(Split-Path $l.resourceFile -Leaf)"
        Start-Sleep -Seconds 2
        Invoke-Uninstall PROD
    }
    'a' {
        $e = $Editions.PROD
        Invoke-Pack PROD '0.1.0'; Invoke-Pack PROD '0.1.1'
        $t = Get-Date
        Invoke-Install PROD '0.1.0'
        $first = Wait-Result PROD 'firstrun' $t 30
        $null = Invoke-Probe PROD @('where'); $w = Read-Result PROD 'where'
        $entry = Get-UninstallEntry $e.PackId
        $lnk = Get-StartMenuShortcut $e.Title
        Report 'a install 0.1.0' ((Test-Path (Get-InstallDir $e.PackId)) -and $entry -and $lnk -and $w.installedVersion -eq '0.1.0' -and -not $w.elevated -and $w.appId -eq $e.PackId) `
            "installDir=%LOCALAPPDATA%\$($e.PackId) uninstallEntry=$([bool]$entry) shortcut='$($lnk.Name)' version=$($w.installedVersion) elevated=$($w.elevated) firstRun=$($null -ne $first)"
        Invoke-Install PROD '0.1.1'
        $null = Invoke-Probe PROD @('version'); $v = Read-Result PROD 'version'
        Report 'a upgrade to 0.1.1 by Setup.exe' ($v.installedVersion -eq '0.1.1') "version=$($v.installedVersion)"
        Invoke-Uninstall PROD
        Report 'a uninstall' (-not (Test-Path (Get-InstallDir $e.PackId)) -and -not (Get-UninstallEntry $e.PackId) -and -not (Get-StartMenuShortcut $e.Title)) `
            "installDir gone=$(-not (Test-Path (Get-InstallDir $e.PackId))) entry gone=$(-not (Get-UninstallEntry $e.PackId)) shortcut gone=$(-not (Get-StartMenuShortcut $e.Title))"
    }
    'b' {
        $e = $Editions.PROD
        Invoke-Install PROD '0.1.0'
        $null = Invoke-Probe PROD @('write-marker')
        $trapDir = Join-Path $env:LOCALAPPDATA ($e.PackId.ToLowerInvariant())   # same folder as the packId, case aside
        Assert-SpikeName $trapDir
        New-Item -ItemType Directory -Force $trapDir | Out-Null
        Set-Content (Join-Path $trapDir 'trap.txt') 'S4 trap marker'
        $installDirItem = Get-Item (Get-InstallDir $e.PackId)
        $sameFolder = (Join-Path $installDirItem.FullName 'trap.txt' | Test-Path)
        Invoke-Install PROD '0.1.1'
        $afterUpgrade = (Invoke-Probe PROD @('read-marker')) -eq 0
        Invoke-Uninstall PROD
        $markerSurvives = Test-Path (Join-Path $e.DataRoot 'marker.txt')
        $trapGone = -not (Test-Path (Join-Path $trapDir 'trap.txt'))
        Report 'b data root survives upgrade' $afterUpgrade 'read-marker after Setup 0.1.1 over 0.1.0'
        Report 'b data root survives uninstall' $markerSurvives "marker.txt in the spike data root exists=$markerSurvives"
        Report 'b case-only name lands in the packId folder and is deleted' ($sameFolder -and $trapGone) "trap visible through the packId path=$sameFolder; gone after uninstall=$trapGone"
    }
    'c' {
        Invoke-Pack DEV '0.1.0'
        Invoke-Install PROD '0.1.0'; Invoke-Install DEV '0.1.0'
        $null = Invoke-Probe PROD @('write-marker'); $null = Invoke-Probe DEV @('write-marker')
        $null = Invoke-Probe PROD @('where'); $null = Invoke-Probe DEV @('where')
        $wp = Read-Result PROD 'where'; $wd = Read-Result DEV 'where'
        $lp = Get-StartMenuShortcut $Editions.PROD.Title | Where-Object BaseName -eq $Editions.PROD.Title
        $ld = Get-StartMenuShortcut $Editions.DEV.Title
        Report 'c side by side' ($wp.appId -eq $Editions.PROD.PackId -and $wd.appId -eq $Editions.DEV.PackId -and $wp.dataRoot -ne $wd.dataRoot -and $lp -and $ld -and $wp.edition -eq 'PROD' -and $wd.edition -eq 'DEV') `
            "PROD appId=$($wp.appId) edition=$($wp.edition); DEV appId=$($wd.appId) edition=$($wd.edition); shortcuts='$($lp.Name)','$($ld.Name)'; data roots differ=$($wp.dataRoot -ne $wd.dataRoot)"
        $prodMarker = Get-Content (Join-Path $Editions.PROD.DataRoot 'marker.txt') -Raw
        Invoke-Uninstall DEV
        $prodStill = (Test-Path (Get-InstallDir $Editions.PROD.PackId)) -and ((Invoke-Probe PROD @('read-marker')) -eq 0) -and ((Get-Content (Join-Path $Editions.PROD.DataRoot 'marker.txt') -Raw) -eq $prodMarker)
        Report 'c uninstall DEV leaves PROD intact' ($prodStill -and -not (Test-Path (Get-InstallDir $Editions.DEV.PackId))) "PROD install + marker unchanged=$prodStill; DEV install gone=$(-not (Test-Path (Get-InstallDir $Editions.DEV.PackId)))"
        Invoke-Uninstall PROD
    }
    'g' {
        $e = $Editions.PROD
        Invoke-Install PROD '0.1.0'
        $null = Invoke-Probe PROD @('write-marker')
        $local = Join-Path $Feed 'PROD'
        $null = Invoke-Probe PROD @('check-update', '--feed', $local)
        $c = Read-Result PROD 'check-update'
        $null = Invoke-Probe PROD @('version'); $v0 = Read-Result PROD 'version'
        Report 'g found, not applied' ($c.found -eq '0.1.1' -and -not $c.applied -and $v0.installedVersion -eq '0.1.0') "found=$($c.found) base=$($c.baseRelease) deltas=$(@($c.deltas).Count) fullSize=$($c.fullSize) still=$($v0.installedVersion)"
        $t = Get-Date
        $null = Invoke-Probe PROD @('check-update', '--feed', $local, '--apply')
        $v = Wait-Result PROD 'version' $t 120
        $marker = (Invoke-Probe PROD @('read-marker')) -eq 0
        $log = Get-Content (Join-Path $e.DataRoot 'results\velopack.log') -Raw
        Report 'g applied' ($v.installedVersion -eq '0.1.1' -and $marker) "version after restart=$($v.installedVersion) marker=$marker delta in log=$($log -match 'delta')"
        Invoke-Uninstall PROD
    }
    'h' {
        $e = $Editions.PROD
        Invoke-Install PROD '0.1.0'
        $null = Invoke-Probe PROD @('write-marker')
        $null = Invoke-Probe PROD @('check-update', '--feed', 'github')
        $c = Read-Result PROD 'check-update'
        $reqs = @($c.requests)
        $hasToken = ($reqs | ConvertTo-Json -Depth 5) -match 'Authorization|token'
        Report 'h found on GitHub' ($c.found -eq '0.1.1') "found=$($c.found) deltas=$(@($c.deltas).Count) requests=$($reqs.Count)"
        Report 'h anonymous request' (-not $hasToken) (($reqs | ForEach-Object { "$($_.url) UA='$($_.userAgent)' headers=$($_.headers | ConvertTo-Json -Compress)" }) -join ' | ')
        $t = Get-Date
        $null = Invoke-Probe PROD @('check-update', '--feed', 'github', '--apply')
        $v = Wait-Result PROD 'version' $t 300
        $marker = (Invoke-Probe PROD @('read-marker')) -eq 0
        Report 'h applied from GitHub' ($v.installedVersion -eq '0.1.1' -and $marker) "version after restart=$($v.installedVersion) marker=$marker"
        Invoke-Uninstall PROD
    }
    'sizes' {
        foreach ($ed in 'PROD', 'DEV') {
            $dir = Join-Path $Feed $ed
            if (Test-Path $dir) {
                Get-ChildItem $dir -Recurse -File -Include '*.nupkg', '*.exe', '*.zip' |
                    ForEach-Object { '{0,-60} {1,8:N1} MB' -f ($_.FullName.Substring($Feed.Length + 1)), ($_.Length / 1MB) }
            }
        }
    }
    default { Write-Host 'Use -Check a|b|c|d|g|h|j|sizes, -SelfTestGuard or -CleanupOnly.' }
}

exit [int]$script:Failed
