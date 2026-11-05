# Tasks

## 1. Roadmap re-plan

- [x] 1.1 Rewrite the table in `docs/roadmap.md` to the shape in design decision 1:
  - add R (in progress, `replan-open-source-delivery`), S4, D, U and v1.0.0;
  - remove morPantry and renumber morFinance to 5;
  - keep the "what's next" procedure and the status values.
  - Verify: the table lists exactly 0, S1-S3, R, S4, D, 1, U, 2, 3, 4, 5, v1.0.0, L, and "what's next" resolves to R today.
- [x] 1.2 Write the new item sections:
  - **[R]** scope: this change.
  - **[S4]** question and checks: install, upgrade and uninstall through an installer; where data lives; DEV next to PROD; the language override; the folder picker; the SmartScreen experience unsigned; update from a GitHub Release and from a local test feed. Velopack (MIT) is the candidate, with the alternatives named.
  - **[D]** scope: unpackaged app; editions fixed by the build without package identity; `run-dev.ps1` rework; a release workflow on a `v*` tag that must point at `main`, with a manual dry-run mode; version from the tag; `THIRD-PARTY-NOTICES` for shipped binaries; modifies the `app-environments` and `development-workflow` specs.
  - **[U]** scope: startup check against GitHub Releases, on by default, can be turned off in Settings, installs only after the user clicks "Update", sends no user data.
  - **[v1.0.0]**: the first and only planned release, once Vehicles, Houses and Finance are complete and working.
  - Verify: each section has status, scope and parked notes.
- [x] 1.3 Update the existing sections.
  - **[S3]:** mark the package-only findings superseded by [S4]/[D] (LocalState, AppData redirection, Reset, the signing facts) and keep the rest.
  - **[1] Foundation:** add the Home dashboard frame ("personal household assistant", cards contributed by modules through their own contracts), and note that the storage paths and backup defaults are re-explored after [D].
  - **[2], [4], [5]:** "same features, new UI and UX".
  - **[3] Importer:** a private, owner-only tool kept outside this repository (design decision 1).
  - **[L]:** drop the GitHub item and the MSIX/Store "Release" item. Add: website as the product front page after parity, free OSS code signing (SignPath Foundation) as an option if SmartScreen friction matters, `/ship` without any external reference.
  - Verify: `Select-String` over `docs/roadmap.md` finds no "Pantry", "Store", "Open Food Facts" or "self-signed" outside the superseded [S3] notes.

## 2. Scrub the tracked content

- [x] 2.1 Create the local deny list `.public-safety.local.txt`, add it to `.gitignore`, and fill it with the owner's real name, personal e-mail, Windows username, local folder names, and the earlier web app's internal terms (from the local notes).
  - Verify: `git check-ignore .public-safety.local.txt` prints the file, and `git status` does not list it.
- [x] 2.2 Scrub `docs/roadmap.md` and `docs/architecture.md` (design decision 2):
  - user paths, the redirected Documents folder name and package hashes become placeholders;
  - legacy port references, the S2 "Old column" column and the importer mechanics go;
  - the [S1]-[S3] outcomes keep their substance.
  - Verify: the deny-list search in task 2.4 has no hits in either file.
- [x] 2.3 Scrub the archived changes under `openspec/changes/archive/`: the bootstrap proposal, design and tasks, the S2 design, and the S3 tasks (and any other hit). Change only identifying details, never decisions or results.
  - Verify: `git diff --stat` touches only those files, and task 2.4 has no hits there.
- [x] 2.4 Search every tracked file case-insensitively for each deny-list line, plus the generic patterns (user folders, package hashes, e-mail addresses, local project paths).
  - Verify: zero hits remain, apart from placeholders (`<...>`, `%...%`) and the allowed addresses.

## 3. Open-source basics and living docs

- [x] 3.1 Add `LICENSE`: the MIT text, `Copyright (c) 2026 KingMordas`. Verify it is the standard MIT text and names no private person.
- [x] 3.2 Rewrite `README.md`:
  - mordWare as a free personal household assistant for Windows 11 (vehicles, houses, finance; data only on your PC; no account, server or tracking);
  - an as-is / no-warranty / spare-time hobby notice and the MIT license;
  - status and roadmap link;
  - developer setup: SDK, `run-dev.ps1`, `git config core.hooksPath .githooks`, gitleaks, the optional deny list;
  - no mention of the earlier web app.
  - Verify: it covers spec scenario "Visitor reads the terms" and passes task 2.4.
- [x] 3.3 Update `CLAUDE.md`:
  - overview (three modules, household assistant, public MIT hobby project);
  - stack (MSIX today, unpackaged after [D], installer through GitHub Releases, no Store, no signing);
  - guardrail 2 (no Open Food Facts; the update check sends no user data);
  - guardrail 7 (edition fixed by the build; mechanism today is package identity, [D] replaces it);
  - new guardrail 11 (public, as-is, safe to publish, `docs/public-safety.md`, never bypass hooks);
  - the earlier-web-app rule without a path;
  - a Definition-of-done row "Any commit -> safety check green";
  - the workflow note on the public-identity author and the release on a `v*` tag.
  - Verify: it reads consistently with the roadmap, and passes task 2.4.
- [x] 3.4 Update `docs/architecture.md`:
  - mark the MSIX/editions/storage sections "current until [D]";
  - the CI section (the safety job; the private-repo cost note removed);
  - a "Public-safety checks" section describing the script, the hooks and the CI job;
  - the delivery direction (installer, GitHub Releases, tag-triggered release) as planned.
  - Verify: no statement contradicts the roadmap.
- [x] 3.5 Update `openspec/config.yaml` (design decision 7): context without the local path or the MSIX-only wording; artifact rules forbidding machine-specific, personal and earlier-web-app details; a tasks rule "run the public-safety check before archive".
  - Verify: `openspec validate replan-open-source-delivery` still runs, and `openspec instructions tasks --change replan-open-source-delivery --json` shows the new rule.
- [x] 3.6 Check `CHANGELOG.md` for stale wording. Change nothing that is still true; no new entry, since nothing user-visible changes. Verify by reading it.

## 4. Public-safety checklist and check script

- [x] 4.1 Write `docs/public-safety.md`:
  - the checklist (identity, secrets, data, machine details, portability, images, tools, go-public);
  - what each layer enforces;
  - how to set up the hooks and the deny list;
  - the owner's GitHub settings (e-mail privacy, push blocking, profile name, secret scanning and push protection).
  - Verify: it contains no literal forbidden value, only placeholders.
- [x] 4.2 Write `scripts/check-public-safety.ps1` with the `-Staged`, `-MessageFile`, `-All` and `-SelfTest` modes and the checks in design decision 3. Its patterns are built so the script cannot match itself.
  - Verify: it runs under Windows PowerShell 5.1 and `pwsh`, and `-All` over the scrubbed repo passes with the deny list present.
- [x] 4.3 Implement `-SelfTest`. It builds a throwaway git repo in a temp folder, stages one sample per rule (assembled at runtime from fragments), runs the checks, and requires each to be refused with the file and line named, while a clean sample passes. It covers spec scenarios "A secret is staged", "A local path is staged" and "Owner-specific words are caught without being published" (with a temporary deny list), plus a deny-listed author identity.
  - Verify: `-SelfTest` reports every case as expected and deletes its temp folder.
- [x] 4.4 Ask the owner's approval to install gitleaks (MIT) with `winget install Gitleaks.Gitleaks`, then install it.
  - Verify: `gitleaks version` prints a version, and `gitleaks git` over the current local history reports no leaks.
- [x] 4.5 Add `.githooks/pre-commit` and `.githooks/commit-msg` (design decision 4) and set `core.hooksPath` on this clone.
  - Verify: in the self-test repo, or with a staged throwaway file, a commit containing a sample secret is refused, and a commit message with a deny-list word is refused. Discard the throwaway changes afterwards.

## 5. Enforcement in CI and in Claude Code

- [x] 5.1 Add the `public-safety` job to `.github/workflows/ci.yml` (design decision 5): `fetch-depth: 0`, pinned gitleaks with a SHA-256 check, `gitleaks git` over the full history, then `check-public-safety.ps1 -All` and `-SelfTest`, `timeout-minutes: 10`. Update the header comment, which mentions private-repo costs.
  - Verify: the YAML parses, and the same commands run locally and succeed. The real run happens after the push (Workflow follow-up).
  - Result (2026-11-05): the YAML parses (jobs `build-test` 20 min and `public-safety` 10 min); gitleaks 8.30.1 is pinned with its release SHA-256, verified against a fresh download. `-All` and `-SelfTest` pass locally. `-History` was added during apply (design decision 3): locally it refuses every pre-squash commit's identity, as intended, and passes only after the squash in the go-public sequence. gitleaks found no secrets in the local history.
- [x] 5.2 Add the tracked `.claude/settings.json` with a `PreToolUse` hook on Bash and PowerShell that runs `.claude/hooks/guard-public.ps1`. The guard blocks hook bypass (`--no-verify`, `commit -n`), `core.hooksPath` changes and repository visibility changes.
  - Verify: pipe sample tool-input JSON into the guard and check that bypass, hooks-path and visibility commands exit with code 2 and a reason, while ordinary `git commit` / `git status` commands pass. This covers the spec scenario "Bypass attempt by the assistant is blocked".
- [x] 5.3 Confirm `.claude/settings.local.json` stays gitignored, and that the tracked settings contain no machine-specific value. Verify with `git check-ignore` and task 2.4.

## 6. Integration check

- [x] 6.1 Run the full public-safety pass on the final working tree: `check-public-safety.ps1 -All` with the deny list, `-SelfTest`, and `gitleaks dir .`. Then review `docs/public-safety.md` item by item, including the archived changes and every image under `src/` and `spikes/`.
  - Verify: everything passes. Record the result in this task in generic terms only, which covers the spec scenario "Ported behavior is specified on its own".
  - Result (2026-11-05): PASS. `-All` with the private deny list: no findings, in PowerShell 7 and 5.1. `-SelfTest`: 11/11. `gitleaks dir .`: no leaks. Manual review against `docs/public-safety.md`: no personal data, secrets, machine-specific details or earlier-web-app details in docs, archived changes, config, scripts or hooks; the 26 tracked images are generated placeholder logos with no metadata chunks and no embedded text. The commit identity (item 1) is set at archive, and the pre-squash history is replaced in the go-public sequence.
- [x] 6.2 Run `openspec validate replan-open-source-delivery --strict` and `dotnet build MordWare.slnx`, which proves no project file was touched by mistake. Verify both succeed.

## Workflow follow-up

- `/opsx:archive` this change. Its commit uses the public identity (set it repo-locally first, design decision 6 step 1), and the pre-commit hook must pass.
- Go public, following design decision 6. Each step waits for the owner's explicit OK:
  - optional bundle of the old history;
  - squash into "chore: initial public import" and verify that the tree hash matches;
  - prune the old objects;
  - full-history scan plus the manual checklist;
  - add the remote and push;
  - the owner switches visibility to public;
  - the first CI run is green, safety job included. This covers the spec scenarios "Published history shows the public identity", "Clean history passes", "CI scans history" and "Visibility change".
- The owner sets the GitHub e-mail privacy and push-blocking options, decides the profile name, and confirms that secret scanning and push protection are on.
- Then "what's next" is [S4].
