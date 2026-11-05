# Tasks

Hard constraint for every task: **no Windows setting, policy or optional feature on this PC is changed or even attempted.** That includes Smart App Control, SmartScreen, Defender, Developer Mode, Windows Sandbox and execution policy. A step that would need one is recorded as NOT VERIFIED, with the documentation used instead. Results are recorded in generic terms only: no user-folder paths, machine names, package-family hashes or personal values.

## 1. Spike scaffolding

- [ ] 1.1 In `docs/roadmap.md`, mark [S4] `in progress` with change `spike-unpackaged-installer`. Verify that the table and the [S4] section both show it.
- [ ] 1.2 Pin Velopack (latest stable) in `spikes/Directory.Packages.props`, and `vpk` in the local tool manifest `spikes/S4.Installer/.config/dotnet-tools.json` (design D5). Record both versions and confirm both licenses are MIT.
  - Verify:
    - `dotnet tool restore` inside the spike folder installs `vpk` locally, and `dotnet tool list -g` is unchanged
    - `git diff --stat` shows the root `Directory.Packages.props`, `MordWare.slnx` and `src/` unchanged
- [ ] 1.3 Create the probe `spikes/S4.Installer/S4.Installer.csproj` (design D1, D4): unpackaged WinUI 3, self-contained .NET and Windows App SDK, win-x64 only, `MORDWARE_PROD` only in Release, deterministic build with `PathMap` and no PDBs in published output. It is not added to `MordWare.slnx`.
  - Verify:
    - Debug and Release both build
    - `dotnet build MordWare.slnx` is unaffected
- [ ] 1.4 Add the custom entry point (design D2): `DISABLE_XAML_GENERATED_MAIN`, then `Program.Main` running `VelopackApp.Build().Run()`, then command dispatch, then WinUI start. Add the `where` and `version` commands, writing `results\<command>.json` under the edition's spike data root.
  - Verify, from the Debug build output:
    - `where` reports edition DEV, the DEV spike data root and `IsInstalled: false`
    - the window opens with no command (closed right after)
- [ ] 1.5 Add the driver `spikes/S4.Installer/run-checks.ps1` (design D5): `Pack`, `Install`, `Uninstall`, `-CleanupOnly`, and the name guard.
  - Verify:
    - a helper called with `MordWare`, `MordWare.Dev` or a `mordWare Data` path throws before acting
    - `-CleanupOnly` on a clean PC is a no-op that exits 0
- [ ] 1.6 Update the `spikes/` paragraph of `docs/architecture.md` to name `S4.Installer`: an unpackaged probe with Velopack, run on this PC only, not in CI, using spike packIds and spike data roots. Verify that the paragraph matches the files created above.

## 2. Unpackaged runtime (checks d, j)

- [ ] 2.1 Run check (j): from both the build output and an installed copy, Velopack's hook runs before any UI, and the WinUI window then opens normally. Verify with the driver's step: process exit codes, `where.json` written, window title read through the process's `MainWindowTitle`. Close the window afterwards.
- [ ] 2.2 Add the `lang <tag>` command and two `.resw` strings in `Strings/en-US` and `Strings/it-IT` (design D4). Run check (d):
  - the Windows App SDK `PrimaryLanguageOverride` works unpackaged
  - the `.resw` strings load in both languages
  - the `Windows.Globalization` version throws unpackaged (evidence for [D])
  - the edition follows the build constant (Debug = DEV, Release = PROD-like)
  - a run from the build output skips update checks without an error
  - Verify with the driver's PASS / FAIL step, which reads `lang-en-US.json`, `lang-it-IT.json` and `where.json`.

## 3. Install, upgrade, uninstall and data survival (checks a, b, c)

- [ ] 3.1 Pack 0.1.0 (Release, PROD-like) into the local spike feed and run check (a): silent `Setup.exe` install with no elevation prompt and no admin rights, the Start menu entry present, then an upgrade to 0.1.1 over it, then Velopack uninstall. Verify with the driver step:
  - the install folder and the per-user uninstall entry appear, and are gone after uninstall
  - the process ran unelevated
  - `version` reports each version
- [ ] 3.2 Run check (b) (design D3):
  - Write a marker into the spike data root and a trap marker into the lowercase packId folder name.
  - Upgrade, then uninstall.
  - Verify with the driver step: the data-root marker survives both, and the trap marker is gone after uninstall.
- [ ] 3.3 Run check (c): install the DEV-like package (Debug, `MordWare.Spike.S4.Dev`) next to the PROD-like one.
  - Verify with the driver step:
    - two install folders, two data roots, two Start menu entries
    - each `where.json` reports its own edition and root
    - uninstalling DEV leaves the PROD-like install, its marker and its data intact
  - Then uninstall the PROD-like package too.

## 4. Folder picker (check e)

- [ ] 4.1 Add the "Pick folder" and "Write test file" buttons, storing the path in `picker.json` in the spike data root (design D3). Run check (e) with the owner on an installed copy:
  - pick a folder outside Documents and AppData, write
  - close and relaunch, write again
  - Verify that the driver sees both files and that the path persisted. Close the probe window afterwards, and delete the picked test folder in cleanup.

## 5. Package size and binary public safety (check i)

- [ ] 5.1 Add `spikes/S4.Installer/scan-package.ps1` (design D7). Verify it with a self-test: a temp file containing a sample user-folder path in ASCII and in UTF-16LE is reported, and a clean file passes.
- [ ] 5.2 Run check (i) on the 0.1.0 and 0.1.1 full packages, the delta package and `Setup.exe`, with the private deny list present. Verify that no finding comes from the spike's own files; list third-party matches separately as evidence. Any finding in our files blocks group 7 until it is fixed and re-scanned.
- [ ] 5.3 Record the sizes: `Setup.exe`, the full `.nupkg` for each version, and the delta. Note which components dominate (the .NET runtime, the Windows App SDK) as input for [D]'s trimming and ReadyToRun decision.

## 6. Update from a local feed (check g)

- [ ] 6.1 Add the `check-update --feed <path|github> [--apply]` command and the "Check for updates" button (design D6), logging the request URL and headers it sends.
- [ ] 6.2 Run check (g):
  - install 0.1.0 from the local feed and write a marker
  - `check-update` finds 0.1.1 and plans the delta, and nothing is applied without `--apply`
  - `--apply` updates and restarts
  - Verify with the driver step: `version` = 0.1.1, the marker intact, the delta used (Velopack log), no leftover in the data root.

## 7. Update from GitHub pre-releases (check h)

- [ ] 7.1 Before anything is published, reword the release rule in `CLAUDE.md` (Development workflow, Releases) and the matching text in `docs/roadmap.md` to: no `v*.*.*` tags before v1.0.0; the [S4] spike's temporary `s4-feed-*` pre-releases are the one recorded exception, deleted after the check. Verify that both files say the same.
- [ ] 7.2 Ask the owner through AskUserQuestion to approve publishing pre-release `s4-feed-0.1.0` (the exact tag, title, channel `s4-spike` and the files). Only after a yes, run `vpk upload github` (design D6), with the token passed only through the child process's environment. Verify:
  - `gh release view s4-feed-0.1.0` shows it as a pre-release with the expected assets
  - the console output pasted into the notes contains no token
- [ ] 7.3 Ask again through AskUserQuestion for `s4-feed-0.1.1`, then publish it the same way. Verify the same way.
- [ ] 7.4 Run check (h):
  - install 0.1.0, then `check-update --feed github` finds 0.1.1 through the anonymous API with the pre-release and channel filters
  - `--apply` updates
  - Verify with the driver step: `version` = 0.1.1, the marker intact, and the logged request has no token and no user or machine data (guardrail 2).
- [ ] 7.5 Optional, owner-driven (design D8): while 0.1.0 is published, the owner downloads its `Setup.exe` with the browser and runs it, and records what Windows shows. If the owner declines, record NOT VERIFIED (manual).
- [ ] 7.6 Ask the owner through AskUserQuestion to approve deleting both releases and tags. Only after a yes, run `gh release delete s4-feed-<version> --cleanup-tag --yes` for each. Verify:
  - `gh release list` and `git ls-remote --tags origin` show no `s4-feed-*`
  - `git tag -l "s4-feed-*"` is empty locally

## 8. First run of an unsigned installer (check f)

- [ ] 8.1 Read, without changing anything, the Smart App Control state (`VerifiedAndReputablePolicyState`) and the Mark-of-the-Web of a downloaded file if 7.5 ran. From Microsoft's public documentation, record how SmartScreen and Smart App Control treat an unsigned per-user installer and app, citing public URLs.
  - Verify: the notes give PASS / NOT VERIFIED per item, with the source.
  - If Smart App Control would block mordWare with no override, flag it in the roadmap as a decision the owner must take before v1.0.0.
- [ ] 8.2 Draft the user-facing "first install" wording (SmartScreen "More info" -> "Run anyway"; what to do when Smart App Control is on) in the [D] notes of `docs/roadmap.md`, for [D]'s README. Verify that it is in US English and contains nothing machine-specific.

## 9. Outcome, docs and cleanup

- [ ] 9.1 Run `run-checks.ps1 -CleanupOnly`. Verify:
  - no `MordWare.Spike.S4*` install folders, uninstall entries or Start menu shortcuts remain
  - no `mordWare S4 spike*` data roots or local feed remain
  - `MordWare.Dev` is still registered as before the spike
- [ ] 9.2 Update `docs/roadmap.md`:
  - [S4]: `done`, with conditions, the outcome table (a)-(j) and its findings
  - [D]: the adopted settings (unpackaged, self-contained, x64 only, the entry point, packIds `MordWare` / `MordWare.Dev`, the channel, deterministic build plus binary scan, sizes, the first-install wording)
  - [1] Foundation: the data roots `%LOCALAPPDATA%\mordWare Data` and `mordWare Data (DEV)`; settings in a file because `ApplicationData` and `LocalSettings` do not exist unpackaged
  - [U]: the update API, `GithubSource` with no token, default channel, no pre-releases; the rate limit
  - Verify that the table row and every touched section agree.
- [ ] 9.3 Update the "Delivery (planned)" section of `docs/architecture.md` with the decided data roots and installer, and confirm that `CLAUDE.md` describes the delivery model accurately. Verify that `README.md` needs no change, or update it.
- [ ] 9.4 Run `scripts/check-public-safety.ps1 -All` with the local deny list present. Verify that it exits 0 before archive.

## Workflow follow-up

- `/opsx:archive` once every task above is done: mark [S4] done in the roadmap (if not already) and make one Conventional Commit on `main` authored as the public project identity; push only if the owner asks.
