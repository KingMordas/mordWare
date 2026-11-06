# Proposal

## Why

mordWare is still an MSIX app, and an unsigned MSIX cannot be installed ([S3]). [S4] proved the replacement: an unpackaged WinUI 3 app installed and updated per user by Velopack from GitHub Releases. Roadmap item [D] turns that proof into the real app and its release process before Foundation builds persistence on top of it. While exploring [D], the bundled Windows App SDK runtime turned out to be distributed under Microsoft's license terms, not MIT, so [D] also states plainly what mordWare accepts from that runtime, before the first distribution.

## What Changes

- **BREAKING (development): no more MSIX.** The package manifests, the packaged-`dotnet run` support and the Developer Mode requirement are removed. The app runs from its build output (DEV) and from a per-user installer (PROD). It is built for x64 only.
- **Editions without package identity.** The build fixes the edition (Release = PROD, every other configuration = DEV). Each edition has its own name, icon, DEV marker (DEV only) and data folder: `%LOCALAPPDATA%\mordWare Data\` for PROD, `%LOCALAPPDATA%\mordWare Data (DEV)\` for DEV.
- **DEV is never published.** It runs from the build output for daily work. A DEV installer exists only as a local, check-only build for installer and update checks, which never reach PROD.
- **Velopack entry point.** Velopack's startup hooks run before any UI.
- **The app language override uses the Windows App SDK's API**, because the Windows one throws without package identity.
- **A trimmed Windows App SDK.** Only the components the app needs are referenced: no Windows ML, AI, Search or Widgets, which saves about 59 MB.
- **Release workflow.** A pushed `v*.*.*` tag on a commit of `main` builds, tests, packs, scans and publishes the GitHub Release, versioned from the tag. A manual dry run does everything except publish and keeps the installer as a workflow artifact.
- **Every release carries its legal texts:** `LICENSE`, the GPL-3.0 section 7 additional permission and `THIRD-PARTY-NOTICES` (including Microsoft's runtime terms), plus a corresponding-source offer in the release notes.
- **License: an additional permission under GPL-3.0 section 7.** It allows combining mordWare with the Microsoft platform runtime it cannot exist without (the core Windows App SDK and the Windows SDK projection for .NET). `LICENSE` stays the unmodified GPL-3.0 text.
- **Guardrails 1 and 2 reworded** to name that single exception and what the platform runtime may log to Windows. Guardrail 7 now describes the build constant instead of package identity.
- **Binary public safety.** Packages are built deterministically without local paths, and the safety script gains a package-scan mode that runs before any release upload.
- **`run-dev.ps1` reworked:** build Debug, then start the exe. It offers a one-time removal of a leftover MSIX DEV registration.
- **Clean-up:** delete `spikes/S4.Installer`; remove the x86/ARM64 publish profiles; correct the `App (MIT)` package comment; fix the roadmap's [S4] row; move the Smart App Control decision to the v1.0.0 item.

## Capabilities

### New Capabilities
- `app-delivery`: how a user gets, upgrades and removes mordWare (per-user installer, data that survives, the legal texts and source offer each release carries, no network activity of the app's own).

### Modified Capabilities
- `app-environments`: the two editions are told apart by the build, not by an installed identity. DEV runs from the build output and is never published (only a local check-only DEV installer exists). Each edition's data location is defined.
- `development-workflow`: adds the release process (tag on `main` only, version from the tag, dry run, package safety scan before publishing).
- `public-repository`: the license statement adds the GPL-3.0 section 7 additional permission for the Microsoft platform runtime, while the license file stays the unmodified GPL-3.0 text.

## Scope

**In:**
- Unpackaging the app, editions by build constant, the unpackaged data paths, the Velopack entry point and the language-override switch.
- The trimmed Windows App SDK references, and Velopack plus the `vpk` tool (both MIT).
- The release workflow with its dry run, a local pack script (PROD, and the check-only DEV build), and the package scan in `check-public-safety.ps1`.
- `THIRD-PARTY-NOTICES`, the license additional permission, and the guardrail 1, 2 and 7 rewording.
- `run-dev.ps1`, the architecture-test updates, the CI x64 mapping, and the removal of the spike and the MSIX leftovers.
- Docs: `CLAUDE.md`, `README.md` (install, build, license, first-install wording draft), `docs/architecture.md`, `docs/public-safety.md`, `docs/roadmap.md`, `CHANGELOG.md`.

**Out:**
- The startup update check and its UI ([U]).
- Settings, the About page with the legal notices, persistence and backups ([1] Foundation).
- Code signing and the Smart App Control decision (moved to the v1.0.0 item).
- Any real release or `v*.*.*` tag: the dry run is the release workflow's only test before v1.0.0.
- ARM64 or x86 builds, ReadyToRun (stays off), and a portable zip (not shipped).

## Guardrails touched

- **1** (dependencies): reworded for the one named Microsoft platform-runtime exception. Velopack is MIT.
- **2** (no telemetry): reworded. mordWare itself sends nothing; the platform runtime may log to Windows, which the README states. Verified by a no-own-network-connection check.
- **3** (app language): the language override moves to the Windows App SDK API.
- **7** (DEV/PROD): the mechanism changes to a build constant, separate data roots and separate installer identities, and DEV is never published.
- **8** (module boundaries): the architecture tests follow the renamed paths class and the x64 build output, as strict as before.
- **10** (living docs) and **11** (public safety): the package scan, deterministic builds and doc updates.

## User-visible

Yes. mordWare becomes installable through a per-user installer, and the DEV edition is no longer installed as a package. `CHANGELOG.md` `## [Unreleased]` entries are updated accordingly.

## Roadmap

Item **[D] Delivery** in `docs/roadmap.md`.

## Impact

- **Code:** `src/MordWare.App` (project file, entry point, hosting, language override, assets); `src/MordWare.Core/Editions`; `tests/MordWare.Tests.Architecture`; `tests/MordWare.Tests.Unit`.
- **Build:** `Directory.Packages.props` (Windows App SDK components, Velopack; `Microsoft.Windows.SDK.BuildTools.WinApp` removed), `MordWare.slnx` (x64), a root `.config/dotnet-tools.json` for `vpk`.
- **CI and scripts:** `.github/workflows/ci.yml` (x64), a new `.github/workflows/release.yml`, `scripts/run-dev.ps1`, a new `scripts/pack.ps1`, `scripts/check-public-safety.ps1`.
- **Removed:** `Package.appxmanifest`, `Package.Dev.appxmanifest`, the publish profiles, `spikes/S4.Installer`.
- **Legal:** a new section 7 additional-permission file and `THIRD-PARTY-NOTICES`; `README.md` license section.
