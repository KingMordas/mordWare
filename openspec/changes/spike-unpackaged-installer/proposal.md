# Proposal

## Why

Roadmap item [R] decided that mordWare ships as an **unpackaged** WinUI 3 app, installed per user by an **unsigned** installer from **GitHub Releases**, and that it updates itself from there. Today the app cannot run without its MSIX package identity. Its edition, its data folder and its language override all come from that identity. Nothing has yet shown how an unpackaged mordWare installs, upgrades, updates and uninstalls, or where its data must live so that none of these steps can delete it. [D] (delivery) and Foundation (storage) both build on these answers, so they are proven first in throwaway code, the way [S1] to [S3] did.

The 2026-11-05 explore of [S4] settled these decisions, and the spike validates them:

- **Installer and updater: Velopack (MIT).**
  - A per-user `Setup.exe`, with no admin rights, delta packages and an update API.
  - `vpk` packs the releases and uploads them to GitHub.
  - Inno Setup and WiX are fallbacks, considered only if a check fails.
- **DEV runs from the build output** for daily work and is not installed. The spike still installs a DEV-like package next to a PROD-like one through Velopack once, to prove the two never share data (guardrail 7).
- **Live data root, outside the installer's folder:**
  - `%LOCALAPPDATA%\mordWare Data\` for PROD and `%LOCALAPPDATA%\mordWare Data (DEV)\` for DEV. These are siblings, never nested, and they match the S3 backup folder names.
  - Velopack owns `%LOCALAPPDATA%\<packId>\` and deletes it on uninstall, so the data root must never be, or sit inside, a packId folder. Windows paths ignore case, so a case-only difference does not separate them.
  - The spike itself uses only throwaway names. [D] adopts the real ones.
- **Edition from the build.** Release = PROD, every other configuration = DEV, as a build constant, because there is no package identity to read it from.
- **Platform: x64 only.** No ARM64 or x86 build. This is also [D]'s shipping decision; ARM64 PCs run the app under emulation.
- **GitHub update test: one recorded exception to "no tags before v1.0.0".**
  - Temporary **pre-releases on this repository**, tagged `s4-feed-0.1.0` and `s4-feed-0.1.1` (never `v*.*.*`), on a Velopack channel of their own, so no real mordWare build can ever pick them up.
  - Their binaries pass a public-safety scan before upload.
  - Both releases and both tags are deleted afterwards.
  - Every publish and delete step runs only after the owner approves it explicitly at that moment.
- **Nothing on the owner's PC is reconfigured.**
  - No Windows setting is changed or even tried: not Smart App Control, SmartScreen, Defender, Developer Mode, Windows features (Windows Sandbox is not enabled and stays that way) or policies.
  - Checks that would need a setting change are documented from Microsoft's documentation and marked as such.

## What Changes

- **A throwaway spike, `spikes/S4.Installer`.** It is an unpackaged WinUI 3 probe app with Velopack. The PROD-like spike packId is `MordWare.Spike.S4` and the DEV-like one is `MordWare.Spike.S4.Dev`; neither is ever `MordWare` or `MordWare.Dev`. A PowerShell driver packs, installs, updates and uninstalls it and checks the file system from outside. Like S1 to S3, it is not in `MordWare.slnx` and nothing references it.
- **What the spike proves or disproves** (criteria in design.md):
  - **(a)** Install, upgrade over an existing install, and uninstall: per user, no admin, from `Setup.exe` and Velopack's uninstall.
  - **(b)** The data root outside the packId folder survives an upgrade and an uninstall. A marker placed inside the packId folder does not survive, which proves the trap is real.
  - **(c)** The DEV-like and PROD-like packages live side by side, each with its own install folder, data root and Start menu entry. Uninstalling DEV leaves PROD and its data intact.
  - **(d)** The unpackaged runtime works:
    - The Windows App SDK and .NET are bundled with the app.
    - The `.resw` resources load.
    - The app language can be overridden to it-IT and en-US without package identity.
    - The edition comes from the build constant.
    - The app also runs straight from the build output, where updates turn themselves off.
  - **(e)** The Windows App SDK folder picker works unpackaged, and the chosen path is remembered across a restart (rechecks [S3] (e)).
  - **(f)** First run of an unsigned installer: SmartScreen and Smart App Control behavior, taken from Microsoft's documentation and read-only state queries only. Optionally the owner runs a downloaded `Setup.exe` by hand and records what Windows shows. It is written up for users.
  - **(g)** An update from a local feed, 0.1.0 -> 0.1.1:
    - It is found, downloaded as a delta and applied only when asked.
    - The app restarts on 0.1.1 with its data intact.
    - Full and delta package sizes are recorded.
  - **(h)** An update from the temporary GitHub pre-releases: an anonymous request (no token, no user data), the pre-release and channel filters, and the asset layout `vpk upload github` produces. The releases and tags are then deleted.
  - **(i)** Binary public safety: the packages are built deterministically with build paths mapped away, and a scan of every file inside the packages finds no user-folder path, package-family hash or private deny-list word.
  - **(j)** Velopack's startup hook runs first, from the app's own entry point in place of the XAML-generated one, and WinUI then starts normally.
- **Recorded outcome:**
  - PASS / FAIL / NOT VERIFIED per check in the task notes.
  - `docs/roadmap.md`: [S4] done; [D], [1] Foundation and [U] notes updated with the adopted layout, names, build settings and update API.
  - `CLAUDE.md`: the tag rule reworded with the recorded exception.
  - `docs/architecture.md`: the `spikes/` paragraph names S4.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. A spike changes no behavior of the app, so this change sets `skip_specs: true`, as S1 to S3 did. The `app-environments` spec, including dropping DEV's Start menu entry, is rewritten in [D]. The update check gets its spec in [U].

## Scope

**In:**
- The `spikes/S4.Installer` probe app, its driver script and its package scanner.
- Velopack (NuGet) and `vpk` (a .NET tool), pinned only for the spike: in `spikes/Directory.Packages.props` and a tool manifest inside the spike folder.
- Checks (a) to (j), run on this PC, x64 only.
- Two temporary GitHub pre-releases and tags (`s4-feed-*`), created and deleted with the owner's approval at each step.
- Docs: `docs/roadmap.md`, `docs/architecture.md`, `CLAUDE.md`.

**Out:**
- **Left to [D]:**
  - Any change to `src/`, `tests/`, `MordWare.slnx`, the root `Directory.Packages.props`, the app manifests, `scripts/run-dev.ps1` or CI.
  - The release workflow and its dry-run.
  - `THIRD-PARTY-NOTICES` and the GPL source offer.
  - The rewrite of the `app-environments` spec.
  - Folding the binary scan into `scripts/check-public-safety.ps1`.
- **Left to [U]:** the in-app update UI, its Settings switch and localized texts.
- **Left to Foundation:** the real `IAppPaths` implementation and the real data root.
- **Not touched:**
  - The DEV and PROD editions (`MordWare.Dev`, `MordWare`) and their data.
  - The real `mordWare Data` folders and backup folders.
  - Any `v*.*.*` tag.
  - Any Windows setting or optional feature on this PC.

## Guardrails touched

- **1 (OSS only):** Velopack and `vpk` are MIT. The Windows App SDK (MIT) and .NET (MIT) are already in use.
- **2 (no telemetry):** the GitHub update request is anonymous and carries nothing about the user. The spike checks what it sends.
- **6 (no personal data in logs):** the probe logs synthetic values only.
- **7 (DEV and PROD never mix):**
  - spike packIds and data roots only, all deleted at the end
  - the side-by-side check (c)
  - the edition fixed by the build constant
- **11 (public, safe to publish):**
  - the published spike binaries are scanned first (i)
  - the tag rule gets one written, temporary exception
  - every outward step is approved by the owner
  - task notes use generic terms only

## User-visible

No. This is an internal spike, so there is no CHANGELOG entry. The temporary pre-releases are visible on the repository while they exist and are marked as spike pre-releases.

## Roadmap

Item **[S4] Spike: unpackaged app, installer, updates from GitHub**. While applying, its status moves to `in progress` and then `done` with this change's name.

## Impact

- **New, isolated content:** `spikes/S4.Installer`. The app, `src/`, `tests/` and CI are unaffected.
- **Dependencies:** Velopack and `vpk` (MIT), spike-only.
- **Changed docs:** `docs/roadmap.md` ([S4], [D], [1], [U]), `docs/architecture.md` (the `spikes/` paragraph), `CLAUDE.md` (the tag rule).
- **On this PC:** spike packages are installed and uninstalled per user, several times, under `%LOCALAPPDATA%`, with spike-named data roots. Everything is removed at the end. No admin rights and no setting changes.
- **On GitHub:** two short-lived pre-releases and tags on this repository. Anyone watching the repository may be notified when they are published.
