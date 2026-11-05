# Design

## Context

See proposal.md (Why) for the motivation, and `specs/public-repository/spec.md` for the guarantees.

The current state that shapes the approach:

- **History.** 8 local commits, never pushed, authored with the owner's real identity. The remote `KingMordas/mordWare` exists and is empty.
- **Content to scrub.**
  - Machine-specific details: user-folder paths, the name of the redirected Documents folder, package-family hashes, this PC's installed packages. They sit mostly in the archived S3 and bootstrap task notes, plus the roadmap.
  - Absolute paths into the owner's local project folders, and details of the earlier web app. They sit in `CLAUDE.md`, `README.md`, `docs/roadmap.md`, `openspec/config.yaml` and three archived changes (bootstrap proposal, design and tasks; S2 design).
  - No keys, certificates, databases or backups are tracked; `.gitignore` already excludes `*.pfx` and `*.cer`.
- **Tools.** PowerShell 7 and Windows PowerShell 5.1 are available. gitleaks is not installed.
- **CI.** One build-and-test job on `windows-latest`. Standard GitHub-hosted runners are free for public repositories, but the job keeps its timeout.

## Goals / Non-Goals

**Goals:**
- A roadmap and docs that describe only the current plan, with nothing stale and nothing non-public.
- Layered, automatic enforcement of the public-safety rules. No single layer is trusted alone.
- A first public history that contains nothing from before the scrub.

**Non-Goals:**
- Changing app behavior, packaging or editions. That is [D]. Docs describe MSIX as the current state until [D] lands.
- Choosing the installer technology. That is [S4]; Velopack is recorded only as the candidate.
- Contributor process (issue templates, PR rules). A plain as-is notice is enough for a hobby project.

## Decisions

### 1. Roadmap shape

| # | Item | Status |
|---|---|---|
| 0, S1-S3 | as today | done |
| R | Open source and public repository | in progress (this change) |
| S4 | Spike: unpackaged app, installer, updates from GitHub | todo |
| D | Delivery: unpackaged app, editions without package identity, release on `v*` tag, dry-run mode | todo |
| 1 | Foundation (persistence + shell + Home dashboard frame) | todo |
| U | Update check at startup | todo |
| 2 | morVehicles | todo |
| 3 | Importer | todo |
| 4 | morHouses | todo |
| 5 | morFinance | todo |
| v1.0.0 | First and only planned release | todo |
| L | Later / unscheduled | - |

- **Why S4 and D come before Foundation.** Foundation's storage paths, backup defaults and edition checks were designed for package storage. The [S3] items that only hold for packages are marked "superseded by [S4]/[D]", and the rest stays valid: backups in a folder the user chooses, `.tmp` then rename, edition binding, restore with the recovery key, the OneDrive behavior.
- **Why U comes after Foundation.** The update check needs the Settings page for its on/off switch.
- **Why the Importer moves out of this repository.** Its code would reveal the earlier web app's data model, which the spec forbids. It becomes a private, owner-only tool kept outside this repository; its roadmap entry says only that. It consumes mordWare's persistence the way any outside tool would.
- **New UX.** Each module item gets the same note: same features, new UI and UX, and web screens are input, not a template. Foundation's shell includes the Home dashboard frame, and each module contributes cards through a contract it owns (guardrail 8).
- **Alternative considered:** keep the numbering and add D/S4 under [L]. Rejected: "what's next" would then point at Foundation, built on storage assumptions that are about to change.

### 2. Scrubbing: rewrite in place, keep the substance

- **Machine details become generic.**
  - `<user folder>\...` or `%USERPROFILE%\...` for paths.
  - "Documents (redirected into OneDrive on the test PC)" for the Documents folder.
  - `<hash>` for package-family hashes.
  - Facts about the owner's installed packages are dropped.
  - Measured results (PASS/FAIL, timings, row counts) and the OS build stay: they are test conditions, not personal data.
- **Legacy details become mordWare statements or disappear.**
  - The S2 scale table loses its "Old column" column.
  - Port references are replaced by "local notes, outside this repository".
  - The web app's internal names (guards, transaction ids, docs, skills) are removed, and the decisions they led to are stated in mordWare's own terms.
- **Archived changes are edited too.** That breaks "archives are history" in letter. It is accepted: the alternative is publishing the details, and the squash already rewrites history anyway.
- **Alternative considered:** `git filter-repo` to rewrite the content of every commit. Rejected: it needs a new tool, and the squash (decision 6) makes it unnecessary.

### 3. Safety check script: `scripts/check-public-safety.ps1`

Runs on PowerShell 5.1 and 7. It has three modes:
- `-Staged`, used by the pre-commit hook;
- `-MessageFile <path>`, used by the commit-msg hook;
- `-All`, which scans every tracked file and is used in CI and before going public;
- `-History`, which checks the author and committer identity of every commit and runs `gitleaks git` over the full history (CI, and after the squash). Added during apply: the CI history scan needs the identity check as well as gitleaks.

What it refuses:

| Check | Rule (generic, in repo) |
|---|---|
| Identity | The author and committer identities must not contain any deny-list word (the owner's real name and e-mail are in that list). |
| File types | Staged keys, certificates, keystores, `.env`, databases and backup files (`*.pfx`, `*.p12`, `*.cer`, `*.key`, `*.pem`, `*.db`, `*.sqlite*`, mordWare backup extensions). |
| User paths | An absolute path into a Windows or macOS user folder (`<drive>:\Users\<name>`, `/Users/<name>`), except the placeholder forms `<...>` and `%...%`. |
| Package hashes | A package name followed by a 13-character family hash. |
| E-mail addresses | Any address, except `users.noreply.github.com`, `example.com` / `example.org`, and `noreply@anthropic.com` (the co-author trailer). |
| Secrets | gitleaks over the same content: `gitleaks git --pre-commit --staged` for `-Staged`, and the directory scan for `-All`. |
| Owner words | Case-insensitive match against every line of `.public-safety.local.txt` (gitignored): real name, personal e-mail, Windows username, local folder names, the earlier web app's internal terms. |

- **Self-matching.** The script and `docs/public-safety.md` build their patterns so they cannot match themselves. Neither contains a literal example of a forbidden value; examples use placeholders.
- **Missing pieces.** A missing deny list is a warning, so a fresh clone by someone else still works. A missing gitleaks is an error with install instructions: the owner's machine must have it.
- **Errors** name the file and line (or the message line), never the matched secret value in full.
- **Alternative considered:** gitleaks alone. Rejected: it finds secrets, not personal paths, names or legacy terms.

### 4. Git hooks: tracked `.githooks/`

- `pre-commit` and `commit-msg` are small shell shims that call the script with Windows PowerShell or `pwsh`.
- Activation: `git config core.hooksPath .githooks`, documented in the README and set up on the owner's clone during apply.
- **Alternative considered:** the pre-commit framework. Rejected: it adds a Python dependency for two hooks.

### 5. CI and the Claude Code hook

- **CI.** A new `public-safety` job in `ci.yml`, on `windows-latest`, `timeout-minutes: 10`, with `fetch-depth: 0`.
  - It downloads a pinned gitleaks release and checks its SHA-256 against a hash pinned in the workflow.
  - It runs `check-public-safety.ps1 -History` (identities plus `gitleaks git` over the full history, redacted), then `-All` and `-SelfTest` (no deny list in CI).
  - **Alternative considered:** `gitleaks/gitleaks-action`. Rejected: it adds a license-key requirement for some account types and hides the version.
- **Claude Code hook.** A tracked `.claude/settings.json` registers a `PreToolUse` hook on the Bash and PowerShell tools. It runs `.claude/hooks/guard-public.ps1`, which blocks (exit code 2, with a reason) any command that:
  - skips hooks (`--no-verify`, or `commit -n`);
  - changes `core.hooksPath`;
  - changes repository visibility (`gh repo edit ... --visibility`, `gh repo create`, `gh api` calls on the visibility field).
  - The owner performs the visibility change themselves.
- **Owner's GitHub settings** (manual, listed in the checklist):
  - Emails: "Keep my email addresses private" and "Block command line pushes that expose my email".
  - Profile: the display name, the owner's choice.
  - Repository: secret scanning and push protection enabled.

### 6. Go-public sequence (after archive)

1. Set the repo-local identity: `user.name KingMordas` and the GitHub no-reply address. The global config stays untouched.
2. Make the archive commit as usual, so the working tree is final.
3. **Squash:**
   - `git checkout --orphan public-main`, then commit the whole tree as "chore: initial public import" under the public identity;
   - verify that its tree hash equals the old `main` tree;
   - `git branch -M public-main main`.
   - Optionally, first save a local git bundle of the old history to a folder outside the repo, as the owner decides.
4. Drop the old objects: expire the reflog and prune, so no unreachable commit can be pushed by mistake.
5. Scan the new history: `gitleaks git` plus `check-public-safety.ps1 -All` with the deny list. Then review the checklist manually and record the result in the archived change's notes, in generic terms.
6. `git remote add origin https://github.com/KingMordas/mordWare.git`, then `git push -u origin main`, after the owner's OK.
7. The owner sets visibility to public, then the first CI run must be green, safety job included.

Rollback before step 7: delete and recreate the empty remote repository, which is still private. After step 7, there is no rollback for published content, which is why steps 3-5 come first.

### 7. Rules wired into the workflow

- **`CLAUDE.md`.**
  - New guardrail 11: public, as-is, safe to publish, pointing to `docs/public-safety.md`.
  - The earlier-web-app rule loses its path and adds "never put its details in this repository".
  - The Definition of done gains a row: every change → safety check green.
- **`openspec/config.yaml`.**
  - The context drops the local path and the MSIX wording and mentions the public repository.
  - The proposal, specs and design rules gain "contains no machine-specific, personal or earlier-web-app details".
  - The tasks rules gain "run the public-safety check before archive".

## Risks / Trade-offs

- [False positives from the generic patterns block legitimate commits] → Placeholders are allowed; the patterns are narrow (user folders, family hashes); a refusal names the line so the fix is quick. There is no bypass flag by design.
- [The deny list exists only on the owner's PC, so CI cannot check owner words] → The pre-commit hook covers the owner's commits; the go-public scan runs locally with the list; CI covers generic patterns and secrets.
- [Something sensitive survives in a place no pattern covers, such as an image] → The manual checklist review before going public; images only from DEV with synthetic data.
- [Squashing loses the granular history] → The archived OpenSpec changes keep the full story; the history is only 2 days old.
- [Old objects remain in the local repository after the squash] → Step 4 prunes them, and the optional bundle is kept outside the repository.
- [The GitHub profile still shows the owner's real name] → The owner's choice, listed in the checklist; out of the repository's control.
- [Editing archived changes makes them differ from what was originally approved] → Only identifying details change, never decisions or results.

## Open Questions

- Whether to keep a local bundle of the pre-squash history, and for how long. The owner decides at step 3; it changes no task.
- The exact gitleaks version to pin, taken from the latest release at apply time.
