<#
.SYNOPSIS
    Claude Code PreToolUse hook: blocks shell commands that would weaken the public-safety rules
    (CLAUDE.md guardrail 11, docs/public-safety.md).

.DESCRIPTION
    Reads the hook input JSON from stdin. Blocks (exit code 2, reason on stderr) any command that:
      - skips git hooks (--no-verify, or a short-flag cluster with n on git commit);
      - sets or unsets core.hooksPath;
      - changes the repository's visibility or creates a repository (gh repo edit --visibility,
        gh repo create, GitHub API calls touching visibility/private).
    Everything else passes (exit 0). Only the owner performs the blocked actions, outside the assistant.
    Works in Windows PowerShell 5.1 and PowerShell 7+.
#>
$ErrorActionPreference = 'Stop'

function Get-BlockReason {
    param([string]$Command)
    $c = $Command
    if ($c -match '(?i)--no-verify\b') {
        return 'Skipping git hooks (--no-verify) is never allowed: fix the content instead.'
    }
    if ($c -match '(?i)\bgit\b[^|;&\r\n]*\bcommit\b[^|;&\r\n]*\s-(?!-)[a-z]*n[a-z]*(?=\s|$)') {
        return 'Skipping git hooks (git commit -n) is never allowed: fix the content instead.'
    }
    if ($c -match '(?i)core\.hooksPath\s*=' -or
        $c -match '(?i)--unset(-all)?\s+core\.hooksPath' -or
        $c -match '(?i)\bconfig\b[^|;&\r\n]*core\.hooksPath\s+[^\s|;&-][^\s|;&]*') {
        return 'Changing core.hooksPath would disable the public-safety hooks; only the owner may do that.'
    }
    if ($c -match '(?i)\bgh\s+repo\s+(edit\b[^|;&\r\n]*--visibility|create\b)') {
        return 'Changing repository visibility or creating a repository is the owner''s action, not the assistant''s.'
    }
    if ($c -match '(?i)(\bgh\s+api\b|api\.github\.com)' -and $c -match '(?i)\b(visibility|private)\b') {
        return 'Changing repository visibility through the GitHub API is the owner''s action, not the assistant''s.'
    }
    return $null
}

try {
    $raw = [Console]::In.ReadToEnd()
    if (-not $raw) { exit 0 }
    $hookInput = $raw | ConvertFrom-Json
    $command = $null
    if ($hookInput.PSObject.Properties['tool_input'] -and $hookInput.tool_input.PSObject.Properties['command']) {
        $command = [string]$hookInput.tool_input.command
    }
    if (-not $command) { exit 0 }

    $reason = Get-BlockReason $command
    if ($reason) {
        [Console]::Error.WriteLine("Blocked by .claude/hooks/guard-public.ps1 (CLAUDE.md guardrail 11): $reason")
        exit 2
    }
    exit 0
}
catch {
    # Fail closed: a guard that cannot read its input must not let the command through silently.
    [Console]::Error.WriteLine("guard-public.ps1 could not check the command: $($_.Exception.Message)")
    exit 2
}
