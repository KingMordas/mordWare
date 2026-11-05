# Design

## Context

- The repository is empty apart from the OpenSpec scaffolding and the `opsx` commands. Its `openspec/config.yaml` is still the default template.
- The machine has the .NET 10 SDK (10.0.403) and the WinUI Blank App template.
- There is no GitHub remote yet.
- An earlier web app is a read-only reference; its project rules and CI patterns are adapted here, not copied.
- The owner develops on the same PC that runs the real app, so the DEV/PROD split must be structural rather than a convention.

See proposal.md for motivation and scope.

## Goals / Non-Goals

**Goals:**
- The module layout and dependency rules are fixed now, and tests make them fail loudly.
- PROD and DEV are separated by Windows itself (package identity), not by a setting someone could get wrong.
- Every later item starts from `docs/roadmap.md` plus `openspec list`.

**Non-Goals:**
- Any persistence code, including the database environment marker (foundation).
- A real shell UI.
- Release signing.
- Version stamping.

## Decisions

### D1. Solution layout and target frameworks

```
MordWare.slnx
Directory.Build.props        nullable, implicit usings, warnings as errors, LangVersion latest
Directory.Packages.props     central package management (one version per dependency)
src/
  MordWare.Core              net10.0   contracts: AppEnvironment, IAppPaths, ...
  MordWare.Persistence       net10.0   empty (foundation fills it)
  MordWare.Integrations      net10.0   empty
  MordWare.Vehicles          net10.0   Domain/ UseCases/ Data/ (placeholders)
  MordWare.App               net10.0-windows10.0.26100.0, min 10.0.22621.0 (Windows 11)
                             WinUI 3, packaged (MSIX), CommunityToolkit.Mvvm
tests/
  MordWare.Tests.Unit          net10.0
  MordWare.Tests.Architecture  net10.0 (reads the App and unit-test assemblies by path, see D7)
```

Dependency direction:

```
App --> Vehicles --> Core
App --> Persistence --> Core
App --> Integrations --> Core
App --> Core
Vehicles --> Persistence  (allowed: a module's Data/ will use shared persistence infra)
```

- **Libraries target plain `net10.0`, not a Windows TFM.** WinUI types are then unavailable to modules at compile time, which is the first and strongest layer of the "no UI in domain" rule. The architecture tests are the second layer.
- **Alternative rejected:** all projects on the Windows TFM. That is simpler, but it lets WinUI leak into modules, where only a test would catch it.

### D2. Module internals (Option A from explore)

- Each module owns three namespaces:
  - `Domain` (pure C#)
  - `UseCases`
  - `Data` (its own DbContext and mappings, from foundation onward)
- All module contexts will share one SQLite file, each with its own migrations history table.
- Cross-module references are plain ids with no database foreign keys.
- Cross-module features go through contracts owned by the providing module.

Bootstrap creates only the folders and namespaces (one marker type each, so the architecture rules have something to bind to).

- **Alternative rejected:** a central DbContext in Persistence. It would make Persistence reference every module and turn it into the coupling hub the architecture is meant to avoid.

### D3. Edition is derived from the package identity

- `MordWare.Core` defines `AppEnvironment { Prod, Dev }` and a pure resolver that maps a package name to an edition:
  - `MordWare` resolves to Prod.
  - `MordWare.Dev` resolves to Dev.
  - Anything else, including no package identity, resolves to Dev.
- The App reads the running package's name once at startup.

Why not a compile-time `#if DEBUG` flag? Two sources of truth could disagree (for example, a Debug build of the PROD manifest). The package identity is what Windows uses to separate storage, so deriving the edition from it makes the UI marker and the data location agree by construction. Unknown or unpackaged resolves to Dev, which means every doubt lands on the safe side.

### D4. Two manifests, selected by build configuration

- The App project holds `Package.appxmanifest` (PROD: name `MordWare`, display "mordWare") and `Package.Dev.appxmanifest` (DEV: name `MordWare.Dev`, display "mordWare (DEV)", DEV logo set).
- An MSBuild condition picks `Package.Dev.appxmanifest` for `Debug` and the PROD manifest only for `Release`. This satisfies "debug and test runs are always DEV".
- The DEV logo set is the PROD logo with a contrasting color band reading "DEV". The logos are placeholders until a real brand asset exists.

Alternative considered: one manifest with token replacement at build time. Rejected because it is harder to read and easy to break silently.

### D5. Data paths behind `IAppPaths`

- `MordWare.Core` declares `IAppPaths`: `DataFolder` and `LogsFolder` for now, with backups added in foundation.
- The App implements it as `PackagedAppPaths` on top of `ApplicationData.Current.LocalFolder`. Windows already scopes that folder per package, so the two editions get different folders automatically.
- Tests use a `TempAppPaths` test helper that creates a unique folder per run and deletes it afterwards.
- An architecture rule forbids test assemblies from depending on `PackagedAppPaths`.
- Live data stays in LocalState. The facts that LocalState is deleted on uninstall and that backups must live outside the package are handled by the MSIX storage spike and foundation, and are recorded on the roadmap.

### D6. Localization

- WinUI `.resw` resources: `Strings/en-US/Resources.resw` and `Strings/it-IT/Resources.resw`.
- At startup the app sets `ApplicationLanguages.PrimaryLanguageOverride` to the selected app language, which defaults to `en-US` until foundation adds the setting. The OS language never decides.
- Only two strings are needed now: the window title and the DEV marker.

### D7. Architecture tests

- Library: **ArchUnitNET** (`TngTech.ArchUnitNET.xUnit`, Apache-2.0). Its rule language covers namespaces as well as assemblies, which the `Domain` rule needs.
- Fallback: NetArchTest.Rules (MIT). If ArchUnitNET fails to load the WinUI App assembly on .NET 10, switch and note it in the design during apply.
- **As built (apply, 2026-11-04):** ArchUnitNET kept. The architecture test project targets plain `net10.0` and takes the App and `MordWare.Tests.Unit` as build-order-only project references (`ReferenceOutputAssembly="false"`), then reads their DLLs from the same-configuration build output by path. ArchUnitNET reads metadata only, so WinUI never has to load. Tests run on Microsoft.Testing.Platform (xUnit v3, selected in `global.json`); focused runs use `dotnet test --project <path> --filter-class "*Name*"`, not `--filter`.
- Rules:
  1. `MordWare.Core` depends on no other `MordWare.*` assembly.
  2. A module (`MordWare.Vehicles` now, and every future `MordWare.<Module>` named in a single list in the test) depends only on Core, Persistence and Integrations, never on App or another module.
  3. Any `*.Domain` namespace depends on neither `Microsoft.EntityFrameworkCore*` nor `Microsoft.UI*` / `Windows.*`.
  4. Test assemblies do not depend on `MordWare.App.PackagedAppPaths`.
  5. Persistence and Integrations depend on no module.
- The list of modules is a single constant in the architecture test project. Adding a module means adding it there, and that step is a definition-of-done row in `CLAUDE.md`.

### D8. CI

- `.github/workflows/ci.yml` runs on push to `main` and on pull requests, with one job on `windows-latest` and `timeout-minutes: 20`.
- Steps: checkout, set up .NET 10, restore with the NuGet cache, `dotnet build -c Release` (MSIX generation off, `GenerateAppxPackageOnBuild=false`), then `dotnet test --no-build`.
- Duplicate runs are cancelled through a concurrency group.
- Kept lean on purpose, because Windows minutes cost extra on private repos.
- The workflow does nothing until the owner creates the GitHub repo. Creating it is not part of this change.

### D9. Project rules and roadmap documents

- **`CLAUDE.md`:**
  - **Identity:** brand vs. code names, and the earlier web app as a read-only reference.
  - **Numbered guardrails:**
    1. OSS-only dependencies
    2. No telemetry; network calls carry no user data
    3. App culture drives every format
    4. ContentDialog only, never system message boxes
    5. Additive, data-safe schema changes, with a backup before migrating
    6. No personal or financial data in logs
    7. DEV/PROD never mix
    8. Module boundaries via contracts and architecture tests
    9. Performance over disk space; no compaction in the first release
    10. Living docs
  - **Definition-of-done table:** CHANGELOG, resw EN+IT, docs and roadmap, architecture-test module list, focused tests.
  - **Workflow section:** OpenSpec flow, with branches and PRs optional in the early phase.
- **`openspec/config.yaml`:** a short `context` that points to `CLAUDE.md` (a pointer, not a copy), plus per-artifact rules adapted from an earlier project:
  - Dropped: the web-only rules, the branch-first task, and `/ship`.
  - Added: DEV/PROD isolation and module-boundary statements in designs, plus a "roadmap updated" task.
- **`docs/roadmap.md`:** a table of contents of the items, then one section per item (status, change, notes, port references), seeded with every decision and parked note from the 2026-11-04 explore session.
- **`docs/architecture.md`:** the D1-D7 shape in prose, so CLAUDE.md can stay an index.

## Risks / Trade-offs

- [Packaged F5 deploys are slower and occasionally fail on a stale registration] → Document the "remove the MordWare.Dev package and redeploy" fix in `docs/architecture.md`. The safety benefit is worth it.
- [`ApplicationLanguages.PrimaryLanguageOverride` persists across runs per package] → Set it at every startup from the app's own setting, so the stored value never matters.
- [The architecture test project must reference a WinUI exe] → If the project reference causes build or runtime-pack problems, load `MordWare.App.dll` from its build output by path instead, and note this in the design.
- [`windows-latest` image changes break CI] → Accepted. Pin later if it becomes noisy.
- [Placeholder icons ship in early DEV builds] → Acceptable; only the owner sees them.

## Migration Plan

Not applicable: a new repository with no data and no installed users. Rollback means deleting the installed packages (`MordWare`, `MordWare.Dev`).
