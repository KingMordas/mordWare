# Proposal

## Why

The plan has changed in ways the repository does not yet reflect, and the repository is about to become public.

- **mordWare becomes a free, open-source hobby project** for anyone, published as-is with no warranty. It is developed in spare time, licensed MIT, and lives in the public GitHub repository `KingMordas/mordWare`.
- **No Microsoft Store and no code signing.** The app will be delivered by an installer from GitHub Releases, linked from the project website. The [S3] spike showed that an unsigned MSIX package cannot be installed, so mordWare will become an unpackaged app. Several current docs (MSIX, package identity, Store, signing) therefore describe a plan that no longer holds.
- **Three modules, not four.** mordWare ports Vehicles, Houses and Finance with a new UI and UX, under a "personal household assistant" identity with a main Home dashboard. Pantry is retired and will not be ported.
- **One release.** A pushed `v*` tag on `main` will publish a release. The only planned release is v1.0.0, once all three modules are complete and working. The app will check GitHub for updates at startup.

Publishing raises a risk that did not exist before: anything committed becomes public forever. The local history contains personal and machine-specific details, and the docs cite internals of the earlier web app. Both must go before the first push, and from then on a hard rule, checked automatically, must keep them out.

## What Changes

- **Re-plan the roadmap.** Insert three new items:
  - **R:** open source and public repository (this change).
  - **S4:** spike on an unpackaged app, an installer and updates from GitHub.
  - **D:** delivery. Unpackage the app, editions without package identity, the release workflow.
  - Add **U** (startup update check) after Foundation, and a **v1.0.0** milestone after Finance.
  - Remove Pantry and renumber Finance.
  - Annotate the [S3] findings that only apply to packaged apps as superseded.
  - Remove the Store and signing plans.
- **Rewrite the living docs so nothing stale or non-public remains:**
  - `CLAUDE.md`: overview, stack, guardrails 2 and 7, and a new guardrail for the public, as-is project and the safety rule. The rule about the earlier web app is rephrased without a path.
  - `README.md`: what mordWare is, the as-is/hobby notice, the license, and the hook setup.
  - `docs/architecture.md`: MSIX sections marked "current until [D]", the CI section, the safety scan.
  - The context in `openspec/config.yaml`, plus rules so that every artifact stays public-safe.
- **Add the open-source basics:** `LICENSE` (MIT, copyright KingMordas) and the as-is, no-warranty, hobby statement.
- **Add a public-safety checklist,** `docs/public-safety.md`, enforced in layers:
  - a tracked pre-commit hook running a repo script (generic patterns, a gitignored local deny-word list, gitleaks);
  - a full-history gitleaks scan in CI;
  - a Claude Code hook that blocks bypassing hooks and changing the repository's visibility;
  - GitHub's push protection and email-privacy settings, which the owner sets.
- **Scrub the existing content, archived OpenSpec changes included:**
  - the Windows user-folder paths and the name of the redirected Documents folder;
  - absolute paths into the owner's local project folders;
  - package-family hashes and details of this PC's installed packages;
  - every detail of the earlier web app.
  - The S1-S3 outcomes keep their substance.
- **After archive, with the owner's explicit OK at each step:**
  - set the repo-local author identity (KingMordas, GitHub noreply address);
  - squash the local history into one clean initial commit;
  - run the full-history scan, connect the remote, push, and make the repository public.
- **Out of scope:**
  - any app code change: packaging, editions, installer, the release workflow and the update check belong to S4, D and U;
  - `THIRD-PARTY-NOTICES` for shipped binaries, which belongs to D;
  - the website.

### Scope

- **In:**
  - roadmap, `CLAUDE.md`, `README.md`, `docs/architecture.md` and `openspec/config.yaml` updates;
  - `LICENSE`, `docs/public-safety.md`, the safety-check script, the git pre-commit hook, the Claude Code hook settings, and the gitleaks CI job;
  - scrubbing tracked files, archived changes included;
  - the post-archive go-public steps.
- **Out:**
  - app or test code, packaging, the release workflow, the update check, third-party notices, website content.

### Guardrails touched

- **1 (free OSS dependencies):** gitleaks, MIT, is a developer and CI tool, not shipped.
- **2 (no telemetry):** Open Food Facts is dropped from the text, and the update check is added as a network call that sends nothing about the user.
- **6 (no personal data in logs):** extended in spirit by the new rule for repository content.
- **7 (DEV/PROD):** rephrased so it no longer depends on package identity, and points to [D].
- **10 (living docs):** this whole change.
- **New guardrail 11:** public, as-is, safe to publish.

### User-visible

No. Nothing in the app changes, so there is no `CHANGELOG.md` entry. `CHANGELOG.md` is checked only for stale wording.

### Roadmap item

New item **[R] Open source and public repository**, which this change completes. It also reshapes the whole roadmap.

## Capabilities

### New Capabilities

- `public-repository`: what the published repository guarantees. It carries an open-source license and an as-is notice, its content stays free of secrets, personal data, machine-specific details and details of the earlier web app, commits carry only the public project identity, and these rules are checked automatically before a commit and in CI.

### Modified Capabilities

(none. The `app-environments` requirements still describe what the code does today; [D] will modify them when packaging changes. `development-workflow`'s CI requirement is unchanged.)

## Impact

- **Docs:** `docs/roadmap.md`, `CLAUDE.md`, `README.md`, `docs/architecture.md`, `openspec/config.yaml`, and the archived change folders under `openspec/changes/archive/` (scrubbed only).
- **New files:** `LICENSE`, `docs/public-safety.md`, `scripts/check-public-safety.ps1`, `.githooks/pre-commit`, `.claude/settings.json` (a tracked hook) with its hook script, and `.gitleaks.toml` if allowlisting is needed.
- **Changed files:** `.github/workflows/ci.yml` gets a secret-scan job; `.gitignore` gets the local deny-word file.
- **Tools:** gitleaks (MIT), installed locally with winget once the owner approves, and in CI.
- **Git:** repo-local identity, `core.hooksPath`, then a history squash, the remote, the push and public visibility. All of this happens after archive, and each step is confirmed by the owner.
- **No app or test code changes.**
