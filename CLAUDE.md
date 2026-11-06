# CLAUDE.md

Guidance for Claude Code when working in this repository. It is the project's living source of truth: keep it current in the same change that alters anything it describes.

## Overview

**mordWare** is a free, open-source (GPL-3.0-only) personal household assistant for Windows 11: Vehicles, Houses and Finance, plus a Home dashboard, with the data only on the user's own PC and no internet-facing surface. It is a hobby project developed in spare time and provided as-is, **closed to contribution**: pull requests are limited to the owner and Issues are off. As the sole copyright holder, the owner may also distribute it under other terms (for example through the Microsoft Store). It ports the features of an earlier web app with a completely new UI and UX.

- **Brand vs. code names.** The brand is always written **mordWare** (lowercase m, capital W). Code uses **MordWare**: namespaces, projects, assemblies (`MordWare.Core`, `MordWare.Vehicles`, `MordWare.App`, ...).
- **Stack.** .NET 10 (`global.json` pins the SDK and selects Microsoft.Testing.Platform). WinUI 3 (the trimmed Windows App SDK components, self-contained, unpackaged, x64) with MVVM via CommunityToolkit.Mvvm. Velopack installs and updates it. From the foundation change onward: EF Core + SQLite, encrypted at rest, with a recovery key.
- **Delivery.** An **unpackaged** app shipped by a per-user Velopack installer on **GitHub Releases** (`scripts/pack.ps1`, `.github/workflows/release.yml`): no code signing, and no Microsoft Store for now (a Store edition stays possible later). DEV runs from its build output and is never published. A pushed `v*.*.*` tag on `main` publishes a release; the first and only planned release is v1.0.0. The app will check GitHub for updates at startup (roadmap [U]).
- **One Windows account = one data set.** There is no login, no profiles, no roles, no admin area and no e-mail. "Household" is the app's tone, not a multi-user feature.
- **The earlier web app is a private, read-only reference.** Never modify it from this repo, never copy code from it in bulk, and never put any of its details in this repository (guardrail 11). Port references live outside the repository.

## Project guardrails (non-negotiable)

1. **Free, OSS-licensed, GPL-3.0-compatible dependencies only.** MIT, Apache-2.0, BSD, MPL-2.0, LGPL or similar; never GPL-2.0-only, proprietary or "non-commercial" licenses. No trials, no paid tiers. Check the license before adding any package (central versions live in `Directory.Packages.props`), and list every redistributed one in `THIRD-PARTY-NOTICES.txt` (an architecture test enforces it). **The single named exception** is the Microsoft platform runtime the app cannot run without: the Windows App SDK components `Microsoft.WindowsAppSDK.Base`, `.Foundation`, `.InteractiveExperiences`, `.WinUI` and `.DWrite`, and the Windows SDK projection for .NET (`Microsoft.Windows.SDK.NET`). They are redistributed under Microsoft's license terms and covered by the GPL-3.0 section 7 additional permission in `LICENSE-EXCEPTION.txt`. Any other non-OSS component is refused; adding a Microsoft component to that list is an owner decision that updates this guardrail, `LICENSE-EXCEPTION.txt` and the notices together.
2. **No telemetry, analytics or tracking.** mordWare itself sends nothing about the user, and any network call (the update check, FX rates, ...) carries nothing about the user. The bundled Microsoft platform runtime may record usage data to Windows, which handles it according to the user's Windows diagnostic-data setting; the README says so.
3. **The app's selected language drives every format.** Every displayed text, date, number and money value follows the app language (en-US or it-IT, see `AppLanguages`), never the OS or a hard-coded format. Every user-facing string exists in both `Strings/en-US` and `Strings/it-IT`. Use US spelling.
4. **No system message boxes in user flows.** Confirmations, alerts and prompts are in-app, localized dialogs (WinUI `ContentDialog`).
5. **Schema changes are additive and data-safe.** They run on the user's PC with no way to repair them remotely. Never drop or rewrite data destructively. Every migration is preceded by an automatic backup (foundation).
6. **Logs carry no personal or financial data.**
7. **DEV and PROD never mix.** The edition is fixed by the build, never by a runtime setting: Debug builds are always DEV, and only Release builds are PROD. The mechanism is a build constant: `MORDWARE_PROD` is defined only for Release, and `EditionProfile` gives each edition its own names: installer packId (`MordWare` / `MordWare.Dev`), Start menu title and data root (`%LOCALAPPDATA%\mordWare Data\` / `%LOCALAPPDATA%\mordWare Data (DEV)\`). DEV runs from its build output and is never published; a DEV installer exists only as a local, check-only build, and installer checks never touch PROD. Code never builds data paths itself: it asks `IAppPaths`. Tests only ever use temporary folders. Any persistence, backup, import or installer design must state how it keeps the two editions apart.
8. **Module boundaries are enforced, not hoped for.**
   - Each module owns `Domain` (pure C#), `UseCases` and `Data`.
   - A module never references another module's internals; cross-module features (including the Home dashboard cards) go through a contract owned by the providing module.
   - The architecture tests (`tests/MordWare.Tests.Architecture`) must stay green, and are never weakened to make a change pass.
9. **Performance over disk space.** Database size is not a concern, so there is no compaction in the first release. Speed is: index for the real queries, and compute stats in memory where data is per-user small.
10. **Living docs.** `CLAUDE.md`, `README.md`, `docs/*.md` and `docs/roadmap.md` are updated in the same change as the code they describe.
11. **Public, as-is, safe to publish.** This repository is public. Nothing personal, secret, machine-specific or about the earlier web app is ever committed: follow [`docs/public-safety.md`](docs/public-safety.md) to the letter. Commits are authored only as the public project identity. Never bypass the hooks (`--no-verify`, `commit -n`), never change `core.hooksPath`, and never change the repository's visibility: only the owner does that.

When in doubt about a new feature, a dependency, a data-flow or schema change, or anything touching these guardrails, ask before implementing.

## Definition of done

A change is done when every row it triggers is satisfied:

| Trigger | Required |
|---|---|
| Any change | Public-safety check green (`scripts/check-public-safety.ps1 -All`) before archive |
| User-visible change | Entry under `CHANGELOG.md` `## [Unreleased]` |
| New or changed user-facing text | Both `Strings/en-US/Resources.resw` and `Strings/it-IT/Resources.resw` |
| New feature module | Project `MordWare.<Module>` with `Domain`/`UseCases`/`Data`, and its name added to `MordWareArchitecture.Modules` |
| Behavior change | Focused tests for it, green |
| Architecture or workflow change | `docs/architecture.md` / this file / `README.md` updated |
| Roadmap item completed or reshaped | `docs/roadmap.md` updated in the same change |

## Development workflow (OpenSpec)

- **The flow is always** `/opsx:explore` -> `/opsx:propose` -> `/opsx:apply` -> `/opsx:archive`. Never skip a step.
- **Early phase: work on `main`.** Branches and PRs are optional. A finished propose commits the change folder to `main`; archive ends with one Conventional Commit on `main`. Adapting the `/ship` skill (branch + PR + squash) is a roadmap item. Push only when the owner asks.
- **Commits.** Conventional Commits (`feat:`, `fix:`, `docs:`, `chore:`, ...), authored as the public project identity (repo-local git config) and passing the `.githooks` checks. `CHANGELOG.md` `## [Unreleased]` is the changelog of record.
- **Releases.** Only a pushed `v*.*.*` tag on a commit of `main` releases: `.github/workflows/release.yml` checks the tag, `main` and the `CHANGELOG.md` section (`scripts/release-prep.ps1`), builds, tests, packs PROD, scans the package and publishes. Its manual dry run (`workflow_dispatch`, version `0.0.0-dryrun.<run>`) does everything except publish and is the workflow's only test before v1.0.0. No `v*.*.*` tags before v1.0.0. The one recorded exception to "no tags" is roadmap [S4]'s temporary `s4-feed-*` pre-releases (change `spike-unpackaged-installer`): never `v*.*.*`, on a Velopack channel of their own, published and deleted only with the owner's approval at each step, and deleted right after the check.
- **"What's next?"**:
  1. Run `openspec list --json`. An active change with open tasks needs **apply**; one with all tasks done needs **archive**.
  2. Otherwise, take the first unfinished item in `docs/roadmap.md`. It needs **explore**, or **propose** if its decisions are already made.
- **Tests.** Run only the focused tests locally (`dotnet test --project <test project> --filter-class "*Name*"`); CI runs the whole suite.

## Architecture notes

- [`docs/architecture.md`](docs/architecture.md): solution layout, module rules, editions (DEV/PROD), data paths, localization, architecture tests, CI, public-safety checks, and how to run the app.
- [`docs/roadmap.md`](docs/roadmap.md): the ordered plan, with status and parked notes.
- [`docs/public-safety.md`](docs/public-safety.md): the publishing checklist and its enforcement.
