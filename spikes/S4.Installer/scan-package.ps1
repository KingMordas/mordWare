<#
.SYNOPSIS
    Binary public-safety scan for the S4 spike packages (design D7, check i).

.DESCRIPTION
    Scans every file of a Velopack package (.nupkg / .zip, expanded to a temp folder), a folder, or a
    single file such as Setup.exe, for content that must never be published (docs/public-safety.md):
      user-path     an absolute path into a Windows or macOS user folder
      this-user     the current user-profile path, user name or computer name
      email         an e-mail address outside the no-reply and example domains (own files only)
      package-hash  a Windows package-family hash (own files only: binary noise matches it elsewhere)
      private-word  a line of the gitignored deny list .public-safety.local.txt (never printed)
    Strings are read three ways from every file: as Latin-1 (ASCII) and as UTF-16LE at both byte offsets.

    "Own" files are the ones produced on this PC: the probe (S4Probe.*) and the package metadata.
    Every finding in an own file, and any this-user or private-word finding anywhere, BLOCKS (exit 1).
    Other findings in third-party binaries (.NET, Windows App SDK, Velopack) are listed as evidence.

    The rules are copies of scripts/check-public-safety.ps1, assembled from fragments so this file
    never matches them itself. A spike never changes production tooling; [D] decides whether to fold
    this into the real check.

.EXAMPLE
    .\scan-package.ps1 -Path "$env:TEMP\mordWare S4 spike feed\PROD"
    .\scan-package.ps1 -SelfTest
#>
[CmdletBinding()]
param(
    [string[]]$Path,
    [switch]$SelfTest,
    [string]$DenyList = (Join-Path $PSScriptRoot '..\..\.public-safety.local.txt')
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$UserPathRegex = '(?i)(?:\b[a-z]:[\\/]+' + 'users[\\/]+|(?<![\w.])/' + 'Users/)(?![<%$])[A-Za-z0-9._-]+'
$PackageHashRegex = '\b[A-Za-z][A-Za-z0-9.]*_(?:[0-9.]+_[a-z0-9]+__)?([a-z0-9]{13})\b'
$EmailRegex = '(?i)[a-z0-9._%+-]{1,64}' + '@' + '[a-z0-9-]+(?:\.[a-z0-9-]+)*\.[a-z]{2,}'
$AllowedEmailDomains = @('users.noreply.github.com', 'example.com', 'example.org', 'example.net')
$OwnFile = '(?i)(^|[\\/])(S4Probe\.[^\\/]*|[^\\/]*\.nuspec|sq\.version|\[Content_Types\]\.xml|[^\\/]*\.psmdcp|[^\\/]*\.rels|RELEASES[^\\/]*|releases\.[^\\/]*\.json|assets\.[^\\/]*\.json)$'

function Hide-Value([string]$Value) {
    if ($Value.Length -le 4) { return '****' }
    $Value.Substring(0, 2) + ('*' * [Math]::Min(8, $Value.Length - 4)) + $Value.Substring($Value.Length - 2)
}

function Get-DenyRegex {
    if (-not (Test-Path -LiteralPath $DenyList)) { Write-Host 'note: no private deny list found; private words are not checked.' -ForegroundColor Yellow; return $null }
    $words = @(Get-Content -LiteralPath $DenyList | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $_.StartsWith('#') })
    $script:DenyWords = $words
    if (-not $words) { return $null }
    [regex]::new(($words | ForEach-Object { [regex]::Escape($_) }) -join '|', 'IgnoreCase')
}

function Get-Texts([byte[]]$Bytes) {
    $latin1 = [System.Text.Encoding]::Latin1.GetString($Bytes)
    $u0 = [System.Text.Encoding]::Unicode.GetString($Bytes)
    $u1 = if ($Bytes.Length -gt 1) { [System.Text.Encoding]::Unicode.GetString($Bytes, 1, $Bytes.Length - 1) } else { '' }
    return @($latin1, $u0, $u1)
}

function Test-Bytes([string]$Name, [byte[]]$Bytes, [regex]$Deny) {
    $own = $Name -match $OwnFile
    $profile = [regex]::Escape($env:USERPROFILE)
    $thisUser = [regex]::new("$profile|\b$([regex]::Escape($env:USERNAME))\b|\b$([regex]::Escape($env:COMPUTERNAME))\b", 'IgnoreCase')
    $found = [System.Collections.Generic.List[object]]::new()
    $seen = @{}
    foreach ($text in Get-Texts $Bytes) {
        foreach ($m in [regex]::Matches($text, $UserPathRegex)) {
            $k = "user-path|$($m.Value)"; if ($seen[$k]) { continue }; $seen[$k] = 1
            $found.Add([pscustomobject]@{ Rule = 'user-path'; File = $Name; Own = $own; Detail = (Hide-Value $m.Value); Blocks = $own })
        }
        foreach ($m in $thisUser.Matches($text)) {
            $k = "this-user|$($m.Value)"; if ($seen[$k]) { continue }; $seen[$k] = 1
            $found.Add([pscustomobject]@{ Rule = 'this-user'; File = $Name; Own = $own; Detail = (Hide-Value $m.Value); Blocks = $true })
        }
        if ($Deny) {
            foreach ($m in $Deny.Matches($text)) {
                $i = [Array]::FindIndex([string[]]$script:DenyWords, [Predicate[string]] { param($w) $w -ieq $m.Value })
                $k = "private-word|$i"; if ($seen[$k]) { continue }; $seen[$k] = 1
                $found.Add([pscustomobject]@{ Rule = 'private-word'; File = $Name; Own = $own; Detail = "private word #$($i + 1)"; Blocks = $true })
            }
        }
        if ($own) {
            foreach ($m in [regex]::Matches($text, $EmailRegex)) {
                $domain = $m.Value.Substring($m.Value.IndexOf('@') + 1).ToLowerInvariant()
                if ($AllowedEmailDomains | Where-Object { $domain -eq $_ -or $domain.EndsWith('.' + $_) }) { continue }
                $k = "email|$($m.Value)"; if ($seen[$k]) { continue }; $seen[$k] = 1
                $found.Add([pscustomobject]@{ Rule = 'email'; File = $Name; Own = $own; Detail = (Hide-Value $m.Value); Blocks = $true })
            }
            foreach ($m in [regex]::Matches($text, $PackageHashRegex)) {
                $h = $m.Groups[1].Value
                if (-not ($h -match '\d' -and $h -match '[a-z]')) { continue }
                $k = "package-hash|$($m.Value)"; if ($seen[$k]) { continue }; $seen[$k] = 1
                $found.Add([pscustomobject]@{ Rule = 'package-hash'; File = $Name; Own = $own; Detail = (Hide-Value $m.Value); Blocks = $true })
            }
        }
    }
    return $found
}

function Expand-Target([string]$Item, [string]$Work) {
    # Yields (display name, full path) for every file to scan; packages are expanded first.
    $items = if (Test-Path -LiteralPath $Item -PathType Container) { Get-ChildItem -LiteralPath $Item -Recurse -File } else { Get-Item -LiteralPath $Item }
    foreach ($f in $items) {
        if ($f.Extension -in '.nupkg', '.zip') {
            $dest = Join-Path $Work ([guid]::NewGuid().ToString('n'))
            [System.IO.Compression.ZipFile]::ExtractToDirectory($f.FullName, $dest)
            foreach ($inner in Get-ChildItem -LiteralPath $dest -Recurse -File) {
                [pscustomobject]@{ Name = "$($f.Name)!$($inner.FullName.Substring($dest.Length + 1))"; Full = $inner.FullName }
            }
        }
        [pscustomobject]@{ Name = $f.Name; Full = $f.FullName }
    }
}

function Invoke-Scan([string[]]$Targets) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $deny = Get-DenyRegex
    $work = Join-Path $env:TEMP ('mordWare S4 spike scan ' + [guid]::NewGuid().ToString('n'))
    New-Item -ItemType Directory $work | Out-Null
    $all = [System.Collections.Generic.List[object]]::new()
    $count = 0
    $script:FullPaths = @{}
    try {
        foreach ($t in $Targets) {
            foreach ($f in Expand-Target $t $work) {
                $count++
                $found = @(Test-Bytes $f.Name ([System.IO.File]::ReadAllBytes($f.Full)) $deny)
                if ($found) {
                    # Keep a copy of flagged files for the upstream comparison below.
                    $keep = Join-Path $work ('flagged-' + [guid]::NewGuid().ToString('n'))
                    Copy-Item -LiteralPath $f.Full $keep
                    $script:FullPaths[$f.Name] = $keep
                }
                foreach ($x in $found) { $all.Add($x) }
            }
        }
        # A finding in a third-party file that is byte-identical to the same file in an upstream package
        # (the local NuGet cache or the .NET packs) is upstream content, not ours: evidence, not a block.
        $upstreamRoots = @((Join-Path $env:USERPROFILE '.nuget\packages'), (Join-Path $env:ProgramFiles 'dotnet\packs')) | Where-Object { Test-Path $_ }
        foreach ($x in @($all | Where-Object { $_.Blocks -and -not $_.Own })) {
            $leaf = ($x.File -split '[\\/!]')[-1]
            $local = $script:FullPaths[$x.File]
            if (-not $local) { continue }
            $hash = (Get-FileHash -LiteralPath $local).Hash
            $up = Get-ChildItem $upstreamRoots -Recurse -Filter $leaf -File -ErrorAction SilentlyContinue | Where-Object { (Get-FileHash -LiteralPath $_.FullName).Hash -eq $hash } | Select-Object -First 1
            if ($up) { $x.Blocks = $false; $x.Detail += ' (file byte-identical to its upstream package)' }
        }
    } finally { Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue }
    $blocking = @($all | Where-Object Blocks)
    $info = @($all | Where-Object { -not $_.Blocks })
    Write-Host "scanned $count files"
    foreach ($b in $blocking) { Write-Host ("[BLOCK] {0} in {1}: {2}" -f $b.Rule, $b.File, $b.Detail) -ForegroundColor Red }
    if ($info) {
        Write-Host "third-party evidence ($($info.Count)), not blocking:"
        $info | Group-Object Rule, Detail | ForEach-Object { "  {0} x{1}, e.g. {2}" -f $_.Name, $_.Count, $_.Group[0].File } | Write-Host
    }
    if ($blocking) { Write-Host "public-safety scan: $($blocking.Count) blocking finding(s)." -ForegroundColor Red; return 1 }
    Write-Host 'public-safety scan: no blocking finding.' -ForegroundColor Green
    return 0
}

function Test-Embedded([string]$Setup, [string]$Nupkg) {
    # Setup.exe is a stub with the full package appended: prove it, so scanning the nupkg covers it.
    $s = [System.IO.File]::ReadAllBytes($Setup); $n = [System.IO.File]::ReadAllBytes($Nupkg)
    if (-not ('S4Bytes' -as [type])) {
        Add-Type 'public static class S4Bytes { public static int IndexOf(byte[] h, byte[] n) { return System.MemoryExtensions.IndexOf(new System.ReadOnlySpan<byte>(h), new System.ReadOnlySpan<byte>(n)); } }'
    }
    return [S4Bytes]::IndexOf($s, $n)
}

if ($SelfTest) {
    $dir = Join-Path $env:TEMP ('mordWare S4 spike scan selftest ' + [guid]::NewGuid().ToString('n'))
    New-Item -ItemType Directory $dir | Out-Null
    try {
        $sample = 'C:' + '\' + 'Users' + '\' + 'sample-person' + '\file.txt'
        [System.IO.File]::WriteAllBytes((Join-Path $dir 'S4Probe.ascii.bin'), [byte[]](0, 1, 2) + [System.Text.Encoding]::ASCII.GetBytes($sample) + [byte[]](0, 0))
        [System.IO.File]::WriteAllBytes((Join-Path $dir 'S4Probe.utf16.bin'), [byte[]](7) + [System.Text.Encoding]::Unicode.GetBytes($sample) + [byte[]](0, 0))
        [System.IO.File]::WriteAllText((Join-Path $dir 'S4Probe.clean.txt'), 'nothing to see: %LOCALAPPDATA%\mordWare S4 spike Data')
        $fail = $false
        foreach ($n in 'S4Probe.ascii.bin', 'S4Probe.utf16.bin') {
            $r = Test-Bytes $n ([System.IO.File]::ReadAllBytes((Join-Path $dir $n))) $null
            $ok = @($r | Where-Object { $_.Rule -eq 'user-path' -and $_.Blocks }).Count -ge 1
            Write-Host ("[{0}] self-test {1} is reported" -f ($(if ($ok) { 'PASS' } else { 'FAIL' })), $n); if (-not $ok) { $fail = $true }
        }
        $r = @(Test-Bytes 'S4Probe.clean.txt' ([System.IO.File]::ReadAllBytes((Join-Path $dir 'S4Probe.clean.txt'))) $null)
        $ok = $r.Count -eq 0
        Write-Host ("[{0}] self-test clean file passes" -f ($(if ($ok) { 'PASS' } else { 'FAIL' }))); if (-not $ok) { $fail = $true }
        exit [int]$fail
    } finally { Remove-Item -LiteralPath $dir -Recurse -Force }
}

if (-not $Path) { Write-Host 'Use -Path <nupkg|exe|folder> ... or -SelfTest.'; exit 2 }
foreach ($p in $Path) {
    foreach ($setup in Get-ChildItem -LiteralPath $p -Recurse -File -Filter '*Setup.exe' -ErrorAction SilentlyContinue) {
        $ver = $setup.BaseName -replace '-Setup$', ''
        $nupkg = Get-ChildItem -LiteralPath (Split-Path $setup.DirectoryName) -Filter "*-$ver-*-full.nupkg" -File -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($nupkg) {
            $at = Test-Embedded $setup.FullName $nupkg.FullName
            Write-Host ("{0}: embeds {1} at offset {2}" -f $setup.Name, $nupkg.Name, $(if ($at -ge 0) { $at } else { 'NOT FOUND' }))
        }
    }
}
exit (Invoke-Scan $Path)
