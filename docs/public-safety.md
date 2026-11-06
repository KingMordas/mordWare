# Public safety

mordWare's repository is public. Anything committed can be copied, cached and indexed forever, even after it is deleted. This page is the hard rule behind CLAUDE.md guardrail 11: **follow it to the letter for every commit, every push and every artifact**, including OpenSpec proposals, designs, specs and task notes.

When in doubt, leave it out and ask the owner.

## Checklist

Run through it before every commit (the git hooks automate most of it) and in full before going public or before any push that adds images or new kinds of files.

1. **Identity.**
   - Commits are authored and committed only as the public project identity, with its GitHub no-reply address, set in the repository's own git config.
   - No real name, personal e-mail, phone, address or other contact anywhere: content, commit messages, license, metadata.
2. **Secrets.**
   - No keys, tokens, passwords, certificates, signing material, recovery keys, DPAPI or key files, or connection strings with real values.
   - Examples use obvious placeholders (`<key>`, `x'<64 hex>'`).
3. **Data.**
   - No databases, backups, exports, logs or dumps.
   - No real vehicles, plates, houses, addresses, bills, amounts or accounts.
   - Test fixtures and samples are synthetic.
4. **Machine.**
   - Nothing that describes the owner's PC: no absolute paths into a user folder, no user or machine names, no local folder layout, no redirected-folder names, no package-family hashes, no list of installed software.
   - Use placeholders such as `<user folder>`, `%USERPROFILE%`, `%LOCALAPPDATA%`, `<hash>`.
   - Test conditions may name the Windows build and the tool versions.
5. **Earlier web app.** No details of the earlier web app that mordWare replaces:
   - no location, repository, file, spec or type names;
   - no data model or columns;
   - no hosting, backup or import mechanics;
   - no statement about who uses it.
   - Specs describe mordWare's behavior on its own terms. The project's public website may be linked as the product's front page.
6. **Portability.** Paths are repository-relative. External references are public URLs. A document must make sense on any machine.
7. **Images.**
   - Screenshots come only from the DEV edition with synthetic data.
   - Check every pixel: window titles, paths, notifications and the taskbar can leak details.
8. **Tools.** The checks below are green. Hooks are never bypassed and `core.hooksPath` is never changed.
9. **Packages.** Anything packed for distribution (an installer, a release) is scanned before it leaves the PC or the workflow:
   - `scripts/pack.ps1` runs `check-public-safety.ps1 -Package` on its output, and stops on any finding. The release workflow publishes only a package that passed.
   - The binaries are deterministic and path-free (`PathMap`, no PDBs), so the app's own files hold no local path.
   - Our own files must be clean; a third-party file passes only if it is byte-identical to its upstream package, and Velopack's per-app binaries only if they add nothing to their vendor binaries (not even text that names the build PC) and contain no private word.
   - Never publish a package by hand, and never publish a DEV package.
10. **Going public, or a first push.**
   - The full-history scan passes (`check-public-safety.ps1 -History` and `-All`, with the private deny list).
   - This checklist is reviewed item by item.
   - The owner approves explicitly.
   - Only the owner changes the repository's visibility.

## Enforcement

| Layer | What it does |
|---|---|
| `scripts/check-public-safety.ps1` | The check itself: identity, forbidden file types, user-folder paths, package-family hashes, e-mail addresses, secrets (gitleaks) and the private deny list. Modes: `-Staged`, `-MessageFile <path>`, `-All`, `-History`, `-Package <folder>`, `-SelfTest`. |
| `scripts/pack.ps1` | Packs an installer, then runs `-Package` on it; the release workflow publishes only after it passes. Try it locally: `.\scripts\pack.ps1 -Edition Dev -Version 0.0.1` (PowerShell 7). |
| `.githooks/pre-commit`, `.githooks/commit-msg` | Run the check on every local commit (staged content, then the message). |
| `.public-safety.local.txt` | Your private deny list, gitignored. |
| CI job `public-safety` | `-History` (every commit's identity, and gitleaks over the full history), then `-All` and `-SelfTest`, on every push and pull request. |
| `.claude/settings.json` + `.claude/hooks/guard-public.ps1` | Stops the coding assistant from bypassing hooks, changing `core.hooksPath` or changing the repository's visibility. |
| GitHub settings | See below; GitHub enforces them server-side. |

Details of each rule are in [`architecture.md`](architecture.md#public-safety-checks).

## Setting up a clone

1. Install gitleaks (MIT): `winget install Gitleaks.Gitleaks`.
2. Enable the hooks: `git config core.hooksPath .githooks`.
3. Set the public identity for this repository only:
   - `git config user.name <public name>`
   - `git config user.email <id>+<public name>@users.noreply.github.com`
4. Optional but recommended: create `.public-safety.local.txt` in the repository root with your private words, one literal per line, `#` for comments, matched case-insensitively. Typical entries: your real name, your personal e-mail, your Windows user name, your PC's name, the names of your local folders.
5. Run `.\scripts\check-public-safety.ps1 -SelfTest`, then `.\scripts\check-public-safety.ps1 -All`.

## The owner's GitHub settings

- **Settings > Emails:** "Keep my email addresses private" and "Block command line pushes that expose my email".
- **Profile:** the public display name is the owner's choice. A public repository links to the profile.
- **Repository > Settings > Security:** secret scanning and push protection enabled.

## When a check refuses a commit

- **Fix the content:** replace the value with a placeholder, remove the file, or reword. The refusal names the file and line.
- **Never** skip the check, and never weaken a rule to make a commit pass. A rule that is genuinely wrong is changed in its own reviewed change.
- **If something sensitive was already pushed:** treat it as published. Rotate any secret at once. Then remove it from the history and force-push, after the owner decides.
