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

    Works in Windows PowerShell 5.1 and PowerShell 7+. Requires git and gitleaks on PATH.
    Exit codes: 0 = clean, 1 = findings, 2 = setup error.

.PARAMETER Staged
    Check the staged changes and the commit identity (the pre-commit hook). This is the default.

.PARAMETER MessageFile
    Check a commit message file (the commit-msg hook).

.PARAMETER All
    Check every tracked file in the working tree (CI, and before going public).

.PARAMETER History
    Check the author and committer identity of every commit, and run gitleaks over the full history.

.PARAMETER SelfTest
    Build a throwaway repository in a temp folder and prove that every rule refuses its sample
    and that a clean sample passes.

.EXAMPLE
    .\scripts\check-public-safety.ps1 -All
#>
[CmdletBinding(DefaultParameterSetName = 'Staged')]
param(
    [Parameter(ParameterSetName = 'Staged')][switch]$Staged,
    [Parameter(ParameterSetName = 'Message', Mandatory = $true)][string]$MessageFile,
    [Parameter(ParameterSetName = 'All', Mandatory = $true)][switch]$All,
    [Parameter(ParameterSetName = 'History', Mandatory = $true)][switch]$History,
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
    param([string]$Rule, [string]$File, [int]$Line, [string]$Detail)
    [pscustomobject]@{ Rule = $Rule; File = $File; Line = $Line; Detail = $Detail }
}

function Get-DenyWords {
    param([string]$Root)
    $path = Join-Path $Root $DenyListName
    if (-not (Test-Path -LiteralPath $path)) { return @() }
    $words = @()
    foreach ($l in [System.IO.File]::ReadAllLines($path)) {
        $w = $l.Trim()
        if ($w -and -not $w.StartsWith('#')) { $words += $w }
    }
    return $words
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
    if ($Text -match $UserPathRegex) {
        $found += New-Finding 'user-path' $File $LineNo ('absolute user-folder path ' + (Hide-Value $Matches[0]))
    }
    foreach ($m in [regex]::Matches($Text, $PackageHashRegex)) {
        $h = $m.Groups[1].Value
        if ($h -match '\d' -and $h -match '[a-z]') {
            $found += New-Finding 'package-hash' $File $LineNo ('package-family hash ' + (Hide-Value $m.Value))
        }
    }
    foreach ($m in [regex]::Matches($Text, $EmailRegex)) {
        $addr = $m.Value.ToLowerInvariant()
        $domain = $addr.Substring($addr.IndexOf('@') + 1)
        $ok = ($AllowedEmails -contains $addr)
        foreach ($d in $AllowedEmailDomains) { if ($domain -eq $d -or $domain.EndsWith('.' + $d)) { $ok = $true } }
        if (-not $ok) { $found += New-Finding 'email' $File $LineNo ('e-mail address ' + (Hide-Value $m.Value)) }
    }
    if ($Text -match $PrivateKeyRegex) {
        $found += New-Finding 'private-key' $File $LineNo 'PEM private-key header'
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
