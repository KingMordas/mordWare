# Proposal

## Why

mordWare is a native Windows rewrite of an earlier web app (see the 2026-11-04 explore session), and the repository is empty. Every later roadmap item depends on decisions this change makes: the solution shape, the module boundaries and the checks that enforce them, how DEV and PROD builds stay apart on the same PC, the project rules, and the shared roadmap. Getting these right before any feature exists is cheap. Changing them after modules hold data is not.

## What Changes

- **New .NET 10 solution `MordWare`** with one project per concern:
  - `MordWare.App`: a packaged WinUI 3 app (MVVM with CommunityToolkit.Mvvm) that opens one empty, localized main window.
  - `MordWare.Core`: shared contracts, starting with the app-environment and app-paths abstractions.
  - `MordWare.Persistence`: shared persistence infrastructure only (empty skeleton for now).
  - `MordWare.Integrations`: empty skeleton.
  - `MordWare.Vehicles`: the first module, an empty skeleton laid out as `Domain/`, `UseCases/` and `Data/`.
  - Test projects for unit tests and architecture tests.
- **Module boundaries enforced by architecture tests**:
  - The app may depend on modules, and modules only on Core and the shared infrastructure.
  - A module never references another module's internals.
  - Domain code never references EF Core or WinUI.
  - Test code never resolves the real app-data location.
- **Two side-by-side editions on one PC.** PROD is package `MordWare`, displayed as "mordWare". DEV is package `MordWare.Dev`, displayed as "mordWare (DEV)", with its own icon and a visible DEV badge. Debug runs are always DEV. Each edition resolves its own data folder, so a DEV run can never reach PROD data.
- **`docs/roadmap.md`**: the shared roadmap and the source of truth for "what's next". It lists bootstrap, three spikes (encryption library, decimal storage, MSIX storage/uninstall), foundation, morVehicles, the importer, morHouses, morPantry and morFinance, each with status, change name, parked notes and port references.
- **Project rules**:
  - `CLAUDE.md` with numbered guardrails and a definition-of-done table.
  - `openspec/config.yaml` with context and per-artifact rules, adapted from an earlier project, not copied.
  - `CHANGELOG.md` with `## [Unreleased]`, `.gitignore`, `README.md`.
- **CI**: one GitHub Actions workflow on a Windows runner that builds and runs the tests, with a timeout on every job. It is inert until a GitHub remote exists.

## Capabilities

### New Capabilities

- `app-environments`: the PROD and DEV editions. Covers their distinct identity and appearance, side-by-side installation, which edition a build produces, and per-edition data locations that can never cross.
- `development-workflow`: how mordWare is developed and verified. Covers the enforced module boundaries, CI (build + tests, bounded run time), the roadmap as the source of "what's next", and the changelog of record.

### Modified Capabilities

None (no specs exist yet).

## Scope

**In:**
- Solution and project skeletons.
- Packaging for both editions (manifest identity, display name, icon, DEV badge).
- The app-paths and environment abstractions, with tests.
- Architecture tests.
- `docs/roadmap.md`, `CLAUDE.md`, `openspec/config.yaml`, `CHANGELOG.md`, `README.md`, `.gitignore`.
- The CI workflow.

**Out:**
- **Foundation:** persistence, the SQLite/EF Core setup, encryption, recovery key, backup/restore, migrations, and the in-database environment marker.
- **Shell:** navigation, theming, the settings page and a language switcher beyond what the empty window needs.
- **Features:** any vehicle feature and the importer.
- **Tooling and release:** adapting the `/ship` skill, creating the GitHub repository or remote, MSIX signing for release, Store submission, and choosing a LICENSE (the repo stays private).

## Guardrails touched

This change creates the guardrails in `CLAUDE.md`. The ones it exercises directly:
- OSS-only dependencies (CommunityToolkit.Mvvm MIT, Windows App SDK MIT, ArchUnitNET Apache-2.0, xUnit Apache-2.0).
- App culture drives all formatting (the window title is localized EN/IT).
- DEV and PROD never mix.
- Module boundaries enforced by tests.
- No telemetry.

No privacy trigger fires: no user data is stored, and there are no network calls and no third parties.

## User-visible

Yes, minimally: an installable app (two editions) that opens an empty window. It gets an "Added" entry under `CHANGELOG.md` `## [Unreleased]`.

## Impact

- **New repository content only.** The earlier web app is untouched.
- **New dependencies:** Windows App SDK, CommunityToolkit.Mvvm, xUnit, ArchUnitNET (or NetArchTest if ArchUnitNET proves awkward on .NET 10, recorded in design).
- **Branches and PRs** are optional in this early phase. The work lands on `main`.
