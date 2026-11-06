<#
.SYNOPSIS
    Refuses content that must never be published in this public repository (docs/public-safety.md).

.DESCRIPTION
    Checks for secrets, personal data, machine-specific details and private words, and fails with
    the file and line of every finding. There is deliberately no switch to skip a rule.

    Rules:
      identity      author/committer identity uses a private word or a non-no-reply e-mail
      file-type     keys, certificates, databases, backups, env files, the private deny list
      user-path     an absolute path into a Windows or macOS user folder
      package-hash  a Windows package-family hash (name followed by a 13-character hash)
      email         an e-mail address outside the no-reply and example domains
      private-key   a PEM private-key header
      private-word  a line of the gitignored deny list .public-safety.local.txt
      gitleaks      any secret gitleaks detects

    -Package scans a packed release instead of the repository (see below).

    Works in Windows PowerShell 5.1 and PowerShell 7+. Requires git and gitleaks on PATH (not for -Package).
    Exit codes: 0 = clean, 1 = findings, 2 = setup error.

.PARAMETER Staged
    Check the staged changes and the commit identity (the pre-commit hook). This is the default.

.PARAMETER MessageFile
    Check a commit message file (the commit-msg hook).

.PARAMETER All
    Check every tracked file in the working tree (CI, and before going public).

.PARAMETER History
    Check the author and committer identity of every commit, and run gitleaks over the full history.

.PARAMETER Package
    Scan packed output before it is published (scripts/pack.ps1 runs it): one or more folders, such as
    the Velopack output and the publish folder. Every .nupkg is expanded and scanned too, and each
    Setup.exe must embed its full package byte for byte.
      Own files (MordWare.*, Assets, the legal texts, package metadata) must pass the user-path,
      this-user (this PC's profile path, user or computer name), package-hash, email, private-key and
      private-word rules, read as ASCII and as UTF-16 text.
      Every other file is third-party and passes only if it is byte-identical (SHA-256) to a file of
      its upstream package in the NuGet global packages folder or the .NET packs folder.
      Velopack-derived binaries (the Setup.exe stub, *_ExecutionStub.exe, Squirrel.exe) pass only if
      every rule hit in them, this-user included, also occurs with the same text in their vendor
      binary of the pinned vpk package, and they contain no private word.
    The accepted third-party files are reported per upstream package.

.PARAMETER SelfTest
    Build a throwaway repository in a temp folder and prove that every rule refuses its sample
    and that a clean sample passes; then the same for -Package on a throwaway package folder.

.EXAMPLE
    .\scripts\check-public-safety.ps1 -All

.EXAMPLE
    .\scripts\check-public-safety.ps1 -Package artifacts\pack\Prod
#>
[CmdletBinding(DefaultParameterSetName = 'Staged')]
param(
    [Parameter(ParameterSetName = 'Staged')][switch]$Staged,
    [Parameter(ParameterSetName = 'Message', Mandatory = $true)][string]$MessageFile,
    [Parameter(ParameterSetName = 'All', Mandatory = $true)][switch]$All,
    [Parameter(ParameterSetName = 'History', Mandatory = $true)][switch]$History,
    [Parameter(ParameterSetName = 'Package', Mandatory = $true)][string[]]$Package,
    [Parameter(ParameterSetName = 'SelfTest', Mandatory = $true)][switch]$SelfTest
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$DenyListName = '.public-safety.local.txt'

# Patterns are assembled from fragments so this file never matches its own rules.
$BS = [char]92
$UserPathRegex = '(?i)(?:\b[a-z]:[\\/]+' + 'users[\\/]+|(?<![\w.])/' + 'Users/)(?![<%$])[A-Za-z0-9._-]+'
$PackageHashRegex = '\b[A-Za-z][A-Za-z0-9.]*_(?:[0-9.]+_[a-z0-9]+__)?([a-z0-9]{13})\b'
$EmailRegex = '(?i)[a-z0-9._%+-]+' + '@' + '[a-z0-9-]+(?:\.[a-z0-9-]+)*\.[a-z]{2,}'
$PrivateKeyRegex = '-----' + 'BEGIN [A-Z ]*PRIVATE ' + 'KEY-----'
$AllowedEmailDomains = @('users.noreply.github.com', 'example.com', 'example.org', 'example.net')
# Parentheses matter: in PowerShell ',' binds tighter than '+'.
$AllowedEmails = @(('noreply' + '@anthropic.com'), ('noreply' + '@github.com'), ('git' + '@github.com'))
$ForbiddenExtensions = @('.pfx', '.p12', '.cer', '.crt', '.der', '.key', '.pem', '.snk', '.jks', '.keystore',
    '.db', '.db-wal', '.db-shm', '.sqlite', '.sqlite3', '.mwbk', '.s3bak', '.bak', '.env', '.publishsettings')
$IdentityEmailSuffixes = @('@users.noreply.github.com')
$IdentityEmailExact = @(('noreply' + '@github.com'))

function Invoke-Native {
    # Runs a native command and returns stdout lines. Windows PowerShell 5.1 turns stderr into
    # terminating errors under 'Stop', so stderr is collected separately as plain text.
    param([string]$Exe, [string[]]$NativeArgs)
    $ErrorActionPreference = 'Continue'
    $stdout = @(); $stderr = @()
    & $Exe @NativeArgs 2>&1 | ForEach-Object {
        if ($_ -is [System.Management.Automation.ErrorRecord]) { $stderr += $_.ToString() } else { $stdout += [string]$_ }
    }
    return [pscustomobject]@{ Code = $LASTEXITCODE; Out = $stdout; Err = $stderr }
}

function Invoke-Git {
    param([string]$Root, [string[]]$GitArgs)
    $r = Invoke-Native 'git' (@('-C', $Root) + $GitArgs)
    if ($r.Code -ne 0) { throw "git $($GitArgs -join ' ') failed: $($r.Err -join ' ')" }
    return $r.Out
}

function New-Finding {
    # Raw is the matched text, used only to compare findings (-Package); it is never printed.
    param([string]$Rule, [string]$File, [int]$Line, [string]$Detail, [string]$Raw = '')
    [pscustomobject]@{ Rule = $Rule; File = $File; Line = $Line; Detail = $Detail; Raw = $Raw }
}

function Get-DenyWords {
    param([string]$Root)
    $path = Join-Path $Root $DenyListName
    # The leading comma keeps an empty list an array: PowerShell turns a returned @() into $null.
    if (-not (Test-Path -LiteralPath $path)) { return ,([string[]]@()) }
    $words = @()
    foreach ($l in [System.IO.File]::ReadAllLines($path)) {
        $w = $l.Trim()
        if ($w -and -not $w.StartsWith('#')) { $words += $w }
    }
    return ,([string[]]$words)
}

function Hide-Value {
    param([string]$Value)
    if ($Value.Length -le 4) { return '****' }
    return $Value.Substring(0, 2) + ('*' * [Math]::Min(8, $Value.Length - 4)) + $Value.Substring($Value.Length - 2)
}

function Test-Line {
    # Returns the findings for one line of text.
    param([string]$File, [int]$LineNo, [string]$Text, [string[]]$DenyWords)
    $found = @()
    foreach ($m in [regex]::Matches($Text, $UserPathRegex)) {
        $found += New-Finding 'user-path' $File $LineNo ('absolute user-folder path ' + (Hide-Value $m.Value)) $m.Value
    }
    foreach ($m in [regex]::Matches($Text, $PackageHashRegex)) {
        $h = $m.Groups[1].Value
        if ($h -match '\d' -and $h -match '[a-z]') {
            $found += New-Finding 'package-hash' $File $LineNo ('package-family hash ' + (Hide-Value $m.Value)) $m.Value
        }
    }
    foreach ($m in [regex]::Matches($Text, $EmailRegex)) {
        $addr = $m.Value.ToLowerInvariant()
        $domain = $addr.Substring($addr.IndexOf('@') + 1)
        $ok = ($AllowedEmails -contains $addr)
        foreach ($d in $AllowedEmailDomains) { if ($domain -eq $d -or $domain.EndsWith('.' + $d)) { $ok = $true } }
        if (-not $ok) { $found += New-Finding 'email' $File $LineNo ('e-mail address ' + (Hide-Value $m.Value)) $addr }
    }
    if ($Text -match $PrivateKeyRegex) {
        $found += New-Finding 'private-key' $File $LineNo 'PEM private-key header' 'private-key'
    }
    for ($i = 0; $i -lt $DenyWords.Count; $i++) {
        if ($Text.IndexOf($DenyWords[$i], [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            $found += New-Finding 'private-word' $File $LineNo ("private word #$($i + 1) of $DenyListName")
        }
    }
    return $found
}

function Test-FileName {
    param([string]$File, [string[]]$DenyWords)
    $found = @()
    $name = [System.IO.Path]::GetFileName($File)
    $ext = [System.IO.Path]::GetExtension($File).ToLowerInvariant()
    if ($name -eq $DenyListName) {
        $found += New-Finding 'file-type' $File 0 'the private deny list must never be committed'
    }
    elseif ($ForbiddenExtensions -contains $ext -or $name.ToLowerInvariant() -like '.env*') {
        $found += New-Finding 'file-type' $File 0 "forbidden file type '$ext'"
    }
    $found += @(Test-Line $File 0 $File $DenyWords | Where-Object { $_.Rule -ne 'email' })
    return $found
}

function Test-Identity {
    param([string]$Label, [string]$Ident, [string[]]$DenyWords, [string]$Where)
    $found = @()
    if ($Ident -notmatch '<([^>]*)>') {
        return @(New-Finding 'identity' $Where 0 "$Label has no e-mail")
    }
    $email = $Matches[1].ToLowerInvariant()
    $ok = $IdentityEmailExact -contains $email
    foreach ($s in $IdentityEmailSuffixes) { if ($email.EndsWith($s)) { $ok = $true } }
    if (-not $ok) {
        $found += New-Finding 'identity' $Where 0 "$Label e-mail $(Hide-Value $email) is not a GitHub no-reply address"
    }
    for ($i = 0; $i -lt $DenyWords.Count; $i++) {
        if ($Ident.IndexOf($DenyWords[$i], [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            $found += New-Finding 'identity' $Where 0 "$Label contains private word #$($i + 1) of $DenyListName"
        }
    }
    return $found
}

function Test-IsBinary {
    param([byte[]]$Bytes)
    $n = [Math]::Min($Bytes.Length, 8000)
    for ($i = 0; $i -lt $n; $i++) { if ($Bytes[$i] -eq 0) { return $true } }
    return $false
}

function Get-Gitleaks {
    $cmd = Get-Command gitleaks -ErrorAction SilentlyContinue
    if (-not $cmd) {
        throw 'gitleaks is not installed. Install it (MIT): winget install Gitleaks.Gitleaks  (docs/public-safety.md)'
    }
    return $cmd.Source
}

function Invoke-GitleaksScan {
    # Returns a finding when gitleaks reports leaks; gitleaks prints the redacted details itself.
    param([string[]]$GitleaksArgs, [string]$Where)
    $exe = Get-Gitleaks
    $r = Invoke-Native $exe ($GitleaksArgs + @('--redact', '--no-banner', '--no-color', '--verbose', '--exit-code', '1'))
    if ($r.Code -eq 1) {
        ($r.Out + $r.Err) | ForEach-Object { Write-Host "  $_" }
        return @(New-Finding 'gitleaks' $Where 0 'gitleaks found a secret (details above, redacted)')
    }
    if ($r.Code -ne 0) { throw "gitleaks failed (exit $($r.Code)): $($r.Err -join ' ')" }
    return @()
}

function Invoke-StagedCheck {
    param([string]$Root)
    $deny = Get-DenyWords $Root
    $found = @()
    $found += Test-Identity 'author' ((Invoke-Git $Root @('var', 'GIT_AUTHOR_IDENT')) -join '') $deny '(commit identity)'
    $found += Test-Identity 'committer' ((Invoke-Git $Root @('var', 'GIT_COMMITTER_IDENT')) -join '') $deny '(commit identity)'

    $names = Invoke-Git $Root @('diff', '--cached', '--name-only', '--diff-filter=ACMR', '--no-renames')
    foreach ($n in @($names)) { if ($n) { $found += Test-FileName $n $deny } }

    $diff = Invoke-Git $Root @('diff', '--cached', '-U0', '--no-color', '--no-ext-diff', '--no-renames', '--diff-filter=ACMR')
    $file = $null; $lineNo = 0
    foreach ($raw in @($diff)) {
        $l = ([string]$raw).TrimEnd("`r")
        if ($l.StartsWith('+++ ')) {
            $file = if ($l -eq '+++ /dev/null') { $null } else { $l.Substring(6) }
            continue
        }
        if ($l -match '^@@ -\d+(?:,\d+)? \+(\d+)(?:,\d+)? @@') { $lineNo = [int]$Matches[1]; continue }
        if ($file -and $l.StartsWith('+')) {
            $found += Test-Line $file $lineNo $l.Substring(1) $deny
            $lineNo++
        }
    }
    $found += Invoke-GitleaksScan @('git', '--pre-commit', '--staged', $Root) '(staged changes)'
    return $found
}

function Invoke-MessageCheck {
    param([string]$Root, [string]$Path)
    $deny = Get-DenyWords $Root
    $found = @()
    $lines = [System.IO.File]::ReadAllLines($Path)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^# -+ >8 -+') { break }
        if ($lines[$i].StartsWith('#')) { continue }
        $found += Test-Line '(commit message)' ($i + 1) $lines[$i] $deny
    }
    return $found
}

function Invoke-AllCheck {
    param([string]$Root)
    $deny = Get-DenyWords $Root
    $found = @()
    $files = Invoke-Git $Root @('ls-files', '--cached')
    foreach ($f in @($files)) {
        if (-not $f) { continue }
        $found += Test-FileName $f $deny
        $full = Join-Path $Root $f
        if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { continue }
        $bytes = [System.IO.File]::ReadAllBytes($full)
        if (Test-IsBinary $bytes) { continue }
        $text = [System.Text.Encoding]::UTF8.GetString($bytes)
        $lines = $text -split "`n"
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $found += Test-Line $f ($i + 1) $lines[$i].TrimEnd("`r") $deny
        }
    }
    # gitleaks over exactly the tracked files, exported to a temp folder (never bin/obj or ignored files).
    $export = Join-Path ([System.IO.Path]::GetTempPath()) ('mordware-safety-' + [guid]::NewGuid().ToString('N'))
    try {
        New-Item -ItemType Directory -Path $export | Out-Null
        $prefix = $export.Replace([string]$BS, '/') + '/'   # git needs forward slashes here
        Invoke-Git $Root @('checkout-index', '--all', "--prefix=$prefix") | Out-Null
        $found += Invoke-GitleaksScan @('dir', $export) '(tracked files)'
    }
    finally {
        if (Test-Path -LiteralPath $export) { Remove-Item -LiteralPath $export -Recurse -Force }
    }
    return $found
}

function Invoke-HistoryCheck {
    param([string]$Root)
    $deny = Get-DenyWords $Root
    $found = @()
    $log = Invoke-Git $Root @('log', '--all', '--format=%h%x09%an <%ae>%x09%cn <%ce>')
    foreach ($row in @($log)) {
        if (-not $row) { continue }
        $parts = ([string]$row).Split("`t")
        $found += Test-Identity 'author' $parts[1] $deny "(commit $($parts[0]))"
        $found += Test-Identity 'committer' $parts[2] $deny "(commit $($parts[0]))"
    }
    $found += Invoke-GitleaksScan @('git', $Root) '(full history)'
    return $found
}

# --- -Package: the packed release -----------------------------------------------------------------

# Files of our own, by path inside the scanned folder or package: they must be clean.
#   "!" separates a .nupkg from a path inside it. setup.ico is the edition icon vpk adds to the package.
$OwnPackageFile = '(?i)(^|[\\/!])(MordWare\.[^\\/]*|LICENSE|LICENSE-EXCEPTION\.txt|THIRD-PARTY-NOTICES\.txt|[^\\/]*\.nuspec|sq\.version|\[Content_Types\]\.xml|[^\\/]*\.psmdcp|[^\\/]*\.rels|RELEASES[^\\/]*|releases\.[^\\/]*\.json|assets\.[^\\/]*\.json|setup\.ico)$|(^|[\\/!])Assets[\\/]'

function Get-PackageTexts {
    # Strings in any file: as Latin-1 (ASCII) and as UTF-16LE at both byte offsets.
    param([byte[]]$Bytes)
    $latin1 = [System.Text.Encoding]::GetEncoding(28591).GetString($Bytes)
    $u0 = [System.Text.Encoding]::Unicode.GetString($Bytes)
    $u1 = if ($Bytes.Length -gt 1) { [System.Text.Encoding]::Unicode.GetString($Bytes, 1, $Bytes.Length - 1) } else { '' }
    return @($latin1, $u0, $u1)
}

function Test-OwnPackageBytes {
    # The findings for one own file; every finding blocks.
    param([string]$Name, [byte[]]$Bytes, [string[]]$DenyWords)
    $found = @()
    $seen = @{}
    $thisUser = '(?i)' + [regex]::Escape($env:USERPROFILE) + '|\b' + [regex]::Escape($env:USERNAME) + '\b|\b' + [regex]::Escape($env:COMPUTERNAME) + '\b'
    foreach ($text in Get-PackageTexts $Bytes) {
        # One finding per distinct match, with its text in Raw, so a Velopack-derived binary can be
        # compared exactly with its vendor binary and no new match hides behind a first one.
        foreach ($m in [regex]::Matches($text, $thisUser)) {
            $k = "this-user|$($m.Value)"
            if (-not $seen.ContainsKey($k)) {
                $seen[$k] = 1
                $found += New-Finding 'this-user' $Name 0 ("this PC's user or computer name " + (Hide-Value $m.Value)) $m.Value
            }
        }
        # Printable runs only (like `strings`): a binary's neighboring bytes, such as a string's length
        # prefix, would otherwise glue onto the text and hide it from the word-boundary rules.
        foreach ($l in ($text -split '[\x00-\x1F\x7F]+|[^\x00-\xFF]+')) {
            if ($l.Length -lt 4) { continue }
            foreach ($f in @(Test-Line $Name 0 $l $DenyWords)) {
                $k = "$($f.Rule)|$($f.Raw)|$($f.Detail)"
                if (-not $seen.ContainsKey($k)) { $seen[$k] = 1; $found += $f }
            }
        }
    }
    return $found
}

function Get-UpstreamRoots {
    $roots = @()
    $roots += if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget\packages' }
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnet) { $roots += Join-Path (Split-Path -Parent $dotnet.Source) 'packs' }
    return @($roots | Where-Object { Test-Path -LiteralPath $_ })
}

function Get-FileSha256 {
    param([string]$Path)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $s = [System.IO.File]::OpenRead($Path)
        try { return [BitConverter]::ToString($sha.ComputeHash($s)).Replace('-', '') } finally { $s.Dispose() }
    }
    finally { $sha.Dispose() }
}

function Find-Bytes {
    # Index of $Needle in $Haystack, or -1 (Windows PowerShell 5.1 has no span search).
    param([byte[]]$Haystack, [byte[]]$Needle)
    if (-not ('MordWareSafetyBytes' -as [type])) {
        Add-Type -TypeDefinition 'public static class MordWareSafetyBytes { public static int IndexOf(byte[] h, byte[] n) { if (n.Length == 0) return 0; for (int i = 0; i <= h.Length - n.Length; i++) { if (h[i] != n[0]) continue; int j = 1; while (j < n.Length && h[i + j] == n[j]) j++; if (j == n.Length) return i; } return -1; } }'
    }
    return [MordWareSafetyBytes]::IndexOf($Haystack, $Needle)
}

function Get-VelopackVendorRoot {
    # The vendor folder of the vpk tool pinned in .config/dotnet-tools.json, in the NuGet packages folder.
    param([string]$RepoRoot, [string[]]$UpstreamRoots)
    $manifest = Join-Path $RepoRoot '.config\dotnet-tools.json'
    if (-not (Test-Path -LiteralPath $manifest)) { return $null }
    $text = [System.IO.File]::ReadAllText($manifest)
    if ($text -notmatch '"vpk"\s*:\s*\{\s*"version"\s*:\s*"([^"]+)"') { return $null }
    foreach ($root in $UpstreamRoots) {
        $vendor = Join-Path $root "vpk\$($Matches[1])\vendor"
        if (Test-Path -LiteralPath $vendor) { return $vendor }
    }
    return $null
}

# Velopack's own binaries that vpk derives per app, so they can never be byte-identical to upstream:
# the Setup.exe stub (installer manifest), the shortcut stub (our exe's icon and version resources) and
# Update.exe (renamed Squirrel.exe, icon set). Each is compared with its vendor binaries instead.
$VelopackDerived = @(
    @{ Pattern = '(?i)Setup\.exe \(stub\)$'; Vendor = 'setup*.exe' },
    @{ Pattern = '(?i)_ExecutionStub\.exe$'; Vendor = 'stub*.exe' },
    @{ Pattern = '(?i)(^|[\\/!])Squirrel\.exe$'; Vendor = 'update*.exe' }
)

function Test-VelopackDerivedBytes {
    # A Velopack-derived binary passes only if it adds no rule hit to its vendor binaries: every hit
    # must also occur, with the same text, in one of them. That includes this-PC text: Velopack's own
    # build path inside its pinned vendor binary is upstream content, even on a build PC whose profile
    # path is the same (a GitHub runner). A private word always blocks.
    param([string]$Name, [byte[]]$Bytes, [string]$VendorFilter, [string]$VendorRoot, [string[]]$DenyWords, [hashtable]$VendorCache)
    if (-not $VendorRoot) {
        return @(New-Finding 'package' $Name 0 'the pinned vpk vendor folder was not found: run dotnet tool restore')
    }
    $vendorFiles = @(Get-ChildItem -LiteralPath $VendorRoot -Filter $VendorFilter -File)
    if (-not $vendorFiles) {
        return @(New-Finding 'package' $Name 0 "no vendor binary $VendorFilter in the pinned vpk package")
    }
    $known = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($v in $vendorFiles) {
        if (-not $VendorCache.ContainsKey($v.FullName)) {
            $VendorCache[$v.FullName] = @(Test-OwnPackageBytes $v.Name ([System.IO.File]::ReadAllBytes($v.FullName)) @())
        }
        foreach ($h in $VendorCache[$v.FullName]) { [void]$known.Add("$($h.Rule)|$($h.Raw)") }
    }
    return @(Test-OwnPackageBytes $Name $Bytes $DenyWords | Where-Object {
            $_.Rule -eq 'private-word' -or -not $known.Contains("$($_.Rule)|$($_.Raw)")
        })
}

function Invoke-PackageCheck {
    param([string[]]$Folders, [string[]]$UpstreamRoots, [string[]]$DenyWords, [string]$VendorRoot)
    $vendorCache = @{}
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $found = @()
    $work = Join-Path ([System.IO.Path]::GetTempPath()) ('mordware-package-scan-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $work | Out-Null
    try {
        # 1. Every file to scan, named by its path inside the scanned folder; packages are expanded.
        $files = @()
        foreach ($folder in $Folders) {
            $base = (Resolve-Path -LiteralPath $folder).Path.TrimEnd([char]92)
            foreach ($f in Get-ChildItem -LiteralPath $base -Recurse -File) {
                $rel = $f.FullName.Substring($base.Length + 1)
                if ($f.Extension -ieq '.zip') {
                    $found += New-Finding 'file-type' $rel 0 'a zip archive (no portable zip is shipped)'
                    continue
                }
                if ($f.Extension -ieq '.nupkg') {
                    $dest = Join-Path $work ([guid]::NewGuid().ToString('N'))
                    [System.IO.Compression.ZipFile]::ExtractToDirectory($f.FullName, $dest)
                    foreach ($inner in Get-ChildItem -LiteralPath $dest -Recurse -File) {
                        $files += [pscustomobject]@{ Name = "$rel!$($inner.FullName.Substring($dest.Length + 1))"; Full = $inner.FullName }
                    }
                    continue
                }
                if ($f.Name -like '*Setup.exe') {
                    # Setup.exe is a stub with the full package appended: the package is scanned on its
                    # own, the stub before it against Velopack's vendor setup binaries.
                    # The folder may keep earlier versions: the newest full package that it embeds counts.
                    $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
                    $at = -1
                    foreach ($nupkg in @(Get-ChildItem -LiteralPath $f.DirectoryName -Filter '*-full.nupkg' -File | Sort-Object LastWriteTimeUtc -Descending)) {
                        if ($nupkg.Length -ge $bytes.Length) { continue }
                        $at = Find-Bytes $bytes ([System.IO.File]::ReadAllBytes($nupkg.FullName))
                        if ($at -ge 0) { break }
                    }
                    if ($at -lt 0) {
                        $found += New-Finding 'package' $rel 0 'Setup.exe does not embed the full package next to it'
                    }
                    else {
                        $stub = New-Object byte[] $at
                        [Array]::Copy($bytes, $stub, $at)
                        $found += @(Test-VelopackDerivedBytes "$rel (stub)" $stub 'setup*.exe' $VendorRoot $DenyWords $vendorCache)
                    }
                    continue
                }
                $files += [pscustomobject]@{ Name = $rel; Full = $f.FullName }
            }
        }

        # 2. Own files must be clean.
        # third-party-notices\ holds byte-identical copies of upstream notices files, never our own.
        $upstreamCopy = '(?i)(^|[\\/!])third-party-notices[\\/]'
        $own = @($files | Where-Object { $_.Name -match $OwnPackageFile -and $_.Name -notmatch $upstreamCopy })
        foreach ($f in $own) {
            $found += @(Test-OwnPackageBytes $f.Name ([System.IO.File]::ReadAllBytes($f.Full)) $DenyWords)
        }

        # 3. Third-party files must be byte-identical to a file of their upstream package.
        $third = @($files | Where-Object { $_.Name -notmatch $OwnPackageFile -or $_.Name -match $upstreamCopy })
        $leaves = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
        foreach ($f in $third) { [void]$leaves.Add([System.IO.Path]::GetFileName($f.Full)) }
        $candidates = @{}
        foreach ($root in $UpstreamRoots) {
            foreach ($path in [System.IO.Directory]::EnumerateFiles($root, '*', [System.IO.SearchOption]::AllDirectories)) {
                $leaf = [System.IO.Path]::GetFileName($path)
                if (-not $leaves.Contains($leaf)) { continue }
                if (-not $candidates.ContainsKey($leaf)) { $candidates[$leaf] = New-Object System.Collections.Generic.List[string] }
                $candidates[$leaf].Add($path)
            }
        }
        $hashCache = @{}
        $accepted = @{}
        foreach ($f in $third) {
            $leaf = [System.IO.Path]::GetFileName($f.Full)
            $hash = Get-FileSha256 $f.Full
            $match = $null
            if ($candidates.ContainsKey($leaf)) {
                foreach ($c in $candidates[$leaf]) {
                    if (-not $hashCache.ContainsKey($c)) { $hashCache[$c] = Get-FileSha256 $c }
                    if ($hashCache[$c] -eq $hash) { $match = $c; break }
                }
            }
            if (-not $match) {
                $derived = @($VelopackDerived | Where-Object { $f.Name -match $_.Pattern } | Select-Object -First 1)
                if ($derived) {
                    $hits = @(Test-VelopackDerivedBytes $f.Name ([System.IO.File]::ReadAllBytes($f.Full)) $derived[0].Vendor $VendorRoot $DenyWords $vendorCache)
                    if ($hits.Count -eq 0) {
                        $package = 'vpk vendor (derived: adds no rule hit)'
                        if (-not $accepted.ContainsKey($package)) { $accepted[$package] = 0 }
                        $accepted[$package]++
                    }
                    $found += $hits
                    continue
                }
                $found += New-Finding 'third-party' $f.Name 0 'not byte-identical to any file of an upstream package'
                continue
            }
            $root = @($UpstreamRoots | Where-Object { $match.StartsWith($_, [StringComparison]::OrdinalIgnoreCase) })[0]
            $parts = $match.Substring($root.Length).TrimStart([char]92, '/').Split([char[]]@([char]92, '/'))
            $package = if ($parts.Count -ge 2) { "$($parts[0])/$($parts[1])" } else { $parts[0] }
            if (-not $accepted.ContainsKey($package)) { $accepted[$package] = 0 }
            $accepted[$package]++
        }

        Write-Host "public-safety: package scan: $($files.Count) files, $($own.Count) own, $($third.Count) third-party."
        if ($accepted.Count -gt 0) {
            Write-Host 'public-safety: third-party files accepted as identical to upstream:'
            foreach ($k in ($accepted.Keys | Sort-Object)) { Write-Host ('  {0,5}  {1}' -f $accepted[$k], $k) }
        }
    }
    finally {
        if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
    }
    return $found
}

function Write-Findings {
    param([object[]]$Findings)
    foreach ($f in $Findings) {
        $loc = if ($f.Line -gt 0) { "$($f.File):$($f.Line)" } else { $f.File }
        Write-Host "public-safety: [$($f.Rule)] ${loc}: $($f.Detail)" -ForegroundColor Red
    }
}

function Invoke-SelfTest {
    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ('mordware-selftest-' + [guid]::NewGuid().ToString('N'))
    $failures = 0
    $cases = 0
    try {
        New-Item -ItemType Directory -Path $tmp | Out-Null
        Invoke-Git $tmp @('init', '-q') | Out-Null
        Invoke-Git $tmp @('config', 'user.name', 'Self Test') | Out-Null
        Invoke-Git $tmp @('config', 'user.email', ('selftest' + '@users.noreply.github.com')) | Out-Null
        Invoke-Git $tmp @('config', 'core.autocrlf', 'false') | Out-Null
        $denyWord = 'Zorblax' + ' Quentin'
        [System.IO.File]::WriteAllText((Join-Path $tmp $DenyListName), "# self-test`n$denyWord`n")

        # Each sample is assembled at runtime and sits on line 2 of its file.
        $ghToken = 'gh' + 'p_' + 'R7mZq2Lx9Vb4Nc8Kd1Wf6Hj3Tp5Ys0Ua2Eg7'
        $pemBody = 'MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQC7' + ('x' * 40)
        $samples = @(
            @{ Name = 'secret.txt'; Rule = 'private-key'; Text = ('-----' + 'BEGIN PRIVATE ' + 'KEY-----') + "`n$pemBody`n" + ('-----' + 'END PRIVATE ' + 'KEY-----') },
            @{ Name = 'token.txt'; Rule = 'gitleaks'; Text = "token = `"$ghToken`"" },
            @{ Name = 'path.md'; Rule = 'user-path'; Text = ('C:' + $BS + 'Users' + $BS + 'jdoe' + $BS + 'Documents' + $BS + 'notes.txt') },
            @{ Name = 'hash.md'; Rule = 'package-hash'; Text = ('Contoso.App' + '_' + 'q4zt8rw2kd7mn') },
            @{ Name = 'mail.md'; Rule = 'email'; Text = ('jane.doe' + '@' + 'contoso.net') },
            @{ Name = 'word.md'; Rule = 'private-word'; Text = "written by $denyWord" },
            @{ Name = 'keys.pfx'; Rule = 'file-type'; Text = 'not a real certificate' }
        )
        foreach ($s in $samples) {
            $cases++
            $path = Join-Path $tmp $s.Name
            [System.IO.File]::WriteAllText($path, "sample`n$($s.Text)`n")
            Invoke-Git $tmp @('add', '--', $s.Name) | Out-Null
            $r = @(Invoke-StagedCheck $tmp 6>$null)
            Invoke-Git $tmp @('rm', '--cached', '-q', '--', $s.Name) | Out-Null
            Remove-Item -LiteralPath $path -Force
            $hit = @($r | Where-Object { $_.Rule -eq $s.Rule -and ($_.Rule -eq 'gitleaks' -or ($_.File -like "*$($s.Name)*" -and ($_.Line -eq 2 -or $_.Rule -eq 'file-type'))) })
            if ($hit.Count -gt 0) { Write-Host "  ok   [$($s.Rule)] $($s.Name) refused" -ForegroundColor Green }
            else { $failures++; Write-Host "  FAIL [$($s.Rule)] $($s.Name) was not refused" -ForegroundColor Red }
        }

        # A private word in the commit identity.
        $cases++
        Invoke-Git $tmp @('config', 'user.name', $denyWord) | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $tmp 'ok.md'), "fine`n")
        Invoke-Git $tmp @('add', '--', 'ok.md') | Out-Null
        $r = @(Invoke-StagedCheck $tmp 6>$null)
        if (@($r | Where-Object Rule -eq 'identity').Count -gt 0) { Write-Host '  ok   [identity] private word in author refused' -ForegroundColor Green }
        else { $failures++; Write-Host '  FAIL [identity] private word in author was not refused' -ForegroundColor Red }
        Invoke-Git $tmp @('config', 'user.name', 'Self Test') | Out-Null

        # A personal e-mail in the commit identity.
        $cases++
        Invoke-Git $tmp @('config', 'user.email', ('self' + '@' + 'contoso.net')) | Out-Null
        $r = @(Invoke-StagedCheck $tmp 6>$null)
        if (@($r | Where-Object Rule -eq 'identity').Count -gt 0) { Write-Host '  ok   [identity] personal e-mail in author refused' -ForegroundColor Green }
        else { $failures++; Write-Host '  FAIL [identity] personal e-mail in author was not refused' -ForegroundColor Red }
        Invoke-Git $tmp @('config', 'user.email', ('selftest' + '@users.noreply.github.com')) | Out-Null

        # A private word in the commit message.
        $cases++
        $msg = Join-Path $tmp 'COMMIT_MSG'
        [System.IO.File]::WriteAllText($msg, "docs: update`n`nThanks $denyWord`n# a comment with $denyWord is ignored`n")
        $r = @(Invoke-MessageCheck $tmp $msg)
        if (@($r | Where-Object { $_.Rule -eq 'private-word' -and $_.Line -eq 3 }).Count -eq 1) { Write-Host '  ok   [private-word] commit message refused' -ForegroundColor Green }
        else { $failures++; Write-Host '  FAIL [private-word] commit message was not refused exactly once' -ForegroundColor Red }

        # A clean sample passes: placeholders and allowed addresses only.
        $cases++
        $clean = "Data lives under %LOCALAPPDATA%" + $BS + "Packages" + $BS + "MordWare_<hash>" + $BS + "LocalState and <user folder>.`n" +
            ('Co-Authored-By: Claude <noreply' + '@anthropic.com>') + "`n" + ('Author: KingMordas <1+KingMordas' + '@users.noreply.github.com>') + "`n"
        [System.IO.File]::WriteAllText((Join-Path $tmp 'ok.md'), $clean)
        Invoke-Git $tmp @('add', '--', 'ok.md') | Out-Null
        $r = @(Invoke-StagedCheck $tmp 6>$null)
        if ($r.Count -eq 0) { Write-Host '  ok   clean sample passes' -ForegroundColor Green }
        else { $failures++; Write-Host '  FAIL clean sample was refused:' -ForegroundColor Red; Write-Findings $r }

        # -Package: a clean own file and an upstream-identical third-party file pass; a planted own file
        # with a user path fails; a third-party file that differs from upstream fails.
        $pkgDir = Join-Path $tmp 'package'
        $upRoot = Join-Path $tmp 'upstream'
        $upDir = Join-Path $upRoot 'some.package\1.0.0\lib'
        New-Item -ItemType Directory -Path $pkgDir, $upDir | Out-Null
        $thirdBytes = [System.Text.Encoding]::ASCII.GetBytes('third-party bytes, author ' + 'dev' + '@' + 'contoso.net')
        [System.IO.File]::WriteAllBytes((Join-Path $upDir 'Upstream.Lib.dll'), $thirdBytes)
        [System.IO.File]::WriteAllBytes((Join-Path $pkgDir 'Upstream.Lib.dll'), $thirdBytes)
        [System.IO.File]::WriteAllText((Join-Path $pkgDir 'MordWare.Clean.txt'), 'data under %LOCALAPPDATA%' + $BS + 'mordWare Data')

        $cases++
        $r = @(Invoke-PackageCheck @($pkgDir) @($upRoot) @() 6>$null)
        if ($r.Count -eq 0) { Write-Host '  ok   [package] clean own file and upstream-identical third-party file pass' -ForegroundColor Green }
        else { $failures++; Write-Host '  FAIL [package] clean package was refused:' -ForegroundColor Red; Write-Findings $r }

        $cases++
        $planted = 'C:' + $BS + 'Users' + $BS + 'jdoe' + $BS + 'source' + $BS + 'MordWare.App.pdb'
        [System.IO.File]::WriteAllBytes((Join-Path $pkgDir 'MordWare.Planted.dll'), [byte[]](0, 1, 2) + [System.Text.Encoding]::Unicode.GetBytes($planted) + [byte[]](0, 0))
        $r = @(Invoke-PackageCheck @($pkgDir) @($upRoot) @() 6>$null)
        if (@($r | Where-Object { $_.Rule -eq 'user-path' -and $_.File -eq 'MordWare.Planted.dll' }).Count -gt 0) { Write-Host '  ok   [package] own file with a user path refused' -ForegroundColor Green }
        else { $failures++; Write-Host '  FAIL [package] own file with a user path was not refused' -ForegroundColor Red }
        Remove-Item -LiteralPath (Join-Path $pkgDir 'MordWare.Planted.dll')

        $cases++
        [System.IO.File]::WriteAllBytes((Join-Path $pkgDir 'Upstream.Lib.dll'), $thirdBytes + [byte[]](1))
        $r = @(Invoke-PackageCheck @($pkgDir) @($upRoot) @() 6>$null)
        if (@($r | Where-Object { $_.Rule -eq 'third-party' -and $_.File -eq 'Upstream.Lib.dll' }).Count -gt 0) { Write-Host '  ok   [package] third-party file differing from upstream refused' -ForegroundColor Green }
        else { $failures++; Write-Host '  FAIL [package] third-party file differing from upstream was not refused' -ForegroundColor Red }
        [System.IO.File]::WriteAllBytes((Join-Path $pkgDir 'Upstream.Lib.dll'), $thirdBytes)

        # A Velopack-derived binary passes when it adds no hit to its vendor binary, and fails otherwise.
        $vendorDir = Join-Path $tmp 'vendor'
        New-Item -ItemType Directory -Path $vendorDir | Out-Null
        $vendorText = 'built at ' + 'C:' + $BS + 'Users' + $BS + 'runner' + $BS + 'work, contact ' + 'crate' + '@' + 'contoso.org'
        [System.IO.File]::WriteAllText((Join-Path $vendorDir 'stub.exe'), $vendorText)
        [System.IO.File]::WriteAllText((Join-Path $pkgDir 'mordWare_ExecutionStub.exe'), $vendorText + ' plus our icon')
        $cases++
        $r = @(Invoke-PackageCheck @($pkgDir) @($upRoot) @() $vendorDir 6>$null)
        if ($r.Count -eq 0) { Write-Host '  ok   [package] Velopack-derived binary with only vendor hits passes' -ForegroundColor Green }
        else { $failures++; Write-Host '  FAIL [package] Velopack-derived binary with only vendor hits was refused:' -ForegroundColor Red; Write-Findings $r }

        $cases++
        [System.IO.File]::WriteAllText((Join-Path $pkgDir 'mordWare_ExecutionStub.exe'), $vendorText + ' and ' + 'C:' + $BS + 'Users' + $BS + 'jdoe' + $BS + 'x')
        $r = @(Invoke-PackageCheck @($pkgDir) @($upRoot) @() $vendorDir 6>$null)
        if (@($r | Where-Object { $_.Rule -eq 'user-path' -and $_.File -eq 'mordWare_ExecutionStub.exe' }).Count -eq 1) { Write-Host '  ok   [package] Velopack-derived binary adding a hit refused' -ForegroundColor Green }
        else { $failures++; Write-Host '  FAIL [package] Velopack-derived binary adding a hit was not refused exactly once' -ForegroundColor Red }

        # This PC's profile path inside the vendor binary (as on a build PC whose profile equals
        # Velopack's own build path) is vendor content; this PC's computer name that the vendor binary
        # lacks still blocks, and a private word blocks even when the vendor binary has it too.
        $vendorWithProfile = $vendorText + ' panicked at ' + $env:USERPROFILE + $BS + '.cargo' + $BS + 'registry'
        [System.IO.File]::WriteAllText((Join-Path $vendorDir 'stub.exe'), $vendorWithProfile)
        [System.IO.File]::WriteAllText((Join-Path $pkgDir 'mordWare_ExecutionStub.exe'), $vendorWithProfile + ' plus our icon')
        $cases++
        $r = @(Invoke-PackageCheck @($pkgDir) @($upRoot) @() $vendorDir 6>$null)
        if ($r.Count -eq 0) { Write-Host '  ok   [package] this-PC text that the vendor binary also has passes' -ForegroundColor Green }
        else { $failures++; Write-Host '  FAIL [package] this-PC text that the vendor binary also has was refused:' -ForegroundColor Red; Write-Findings $r }

        $cases++
        [System.IO.File]::WriteAllText((Join-Path $pkgDir 'mordWare_ExecutionStub.exe'), $vendorWithProfile + ' built on ' + $env:COMPUTERNAME)
        $r = @(Invoke-PackageCheck @($pkgDir) @($upRoot) @() $vendorDir 6>$null)
        if (@($r | Where-Object { $_.Rule -eq 'this-user' -and $_.File -eq 'mordWare_ExecutionStub.exe' }).Count -eq 1) { Write-Host '  ok   [package] this-PC text that the vendor binary lacks refused' -ForegroundColor Green }
        else { $failures++; Write-Host '  FAIL [package] this-PC text that the vendor binary lacks was not refused exactly once' -ForegroundColor Red }

        $cases++
        [System.IO.File]::WriteAllText((Join-Path $vendorDir 'stub.exe'), $vendorText + " by $denyWord")
        [System.IO.File]::WriteAllText((Join-Path $pkgDir 'mordWare_ExecutionStub.exe'), $vendorText + " by $denyWord")
        $r = @(Invoke-PackageCheck @($pkgDir) @($upRoot) @($denyWord) $vendorDir 6>$null)
        if (@($r | Where-Object { $_.Rule -eq 'private-word' -and $_.File -eq 'mordWare_ExecutionStub.exe' }).Count -eq 1) { Write-Host '  ok   [package] private word in a Velopack-derived binary refused, even when the vendor has it' -ForegroundColor Green }
        else { $failures++; Write-Host '  FAIL [package] private word in a Velopack-derived binary was not refused' -ForegroundColor Red }
    }
    finally {
        if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Recurse -Force }
    }
    if (Test-Path -LiteralPath $tmp) { Write-Host "  FAIL temp folder not removed: $tmp" -ForegroundColor Red; $failures++ }
    Write-Host "public-safety self-test: $($cases - $failures)/$cases cases as expected"
    return $failures
}

try {
    if ($PSCmdlet.ParameterSetName -eq 'SelfTest') {
        $null = Get-Gitleaks
        $failures = Invoke-SelfTest
        if ($failures -gt 0) { exit 1 } else { exit 0 }
    }

    if ($PSCmdlet.ParameterSetName -eq 'Package') {
        $root = Split-Path -Parent $PSScriptRoot
        $upstream = Get-UpstreamRoots
        $findings = @(Invoke-PackageCheck $Package $upstream (Get-DenyWords $root) (Get-VelopackVendorRoot $root $upstream))
        if ($findings.Count -gt 0) {
            Write-Findings $findings
            Write-Host "public-safety: $($findings.Count) finding(s) in the package. Nothing may be published (docs/public-safety.md)." -ForegroundColor Red
            exit 1
        }
        Write-Host 'public-safety: package check passed.' -ForegroundColor Green
        exit 0
    }

    $top = Invoke-Native 'git' @('rev-parse', '--show-toplevel')
    if ($top.Code -ne 0 -or -not $top.Out) { throw 'Not inside a git repository.' }
    $root = [string]$top.Out[0]

    if (-not (Test-Path -LiteralPath (Join-Path $root $DenyListName))) {
        Write-Host "public-safety: note: no $DenyListName, so private words are not checked (docs/public-safety.md)." -ForegroundColor Yellow
    }

    switch ($PSCmdlet.ParameterSetName) {
        'Message' { $findings = @(Invoke-MessageCheck $root (Resolve-Path -LiteralPath $MessageFile).Path) }
        'All' { $findings = @(Invoke-AllCheck $root) }
        'History' { $findings = @(Invoke-HistoryCheck $root) }
        default { $findings = @(Invoke-StagedCheck $root) }
    }

    if ($findings.Count -gt 0) {
        Write-Findings $findings
        Write-Host "public-safety: $($findings.Count) finding(s). Fix the content; never bypass the check (docs/public-safety.md)." -ForegroundColor Red
        exit 1
    }
    Write-Host "public-safety: $($PSCmdlet.ParameterSetName.ToLowerInvariant()) check passed." -ForegroundColor Green
    exit 0
}
catch {
    Write-Host "public-safety: error: $($_.Exception.Message)" -ForegroundColor Red
    exit 2
}
