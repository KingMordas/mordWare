# Tasks

## 1. Solution scaffolding

- [x] 1.1 Add `.gitignore` (VS/.NET/MSIX outputs, `*.user`, AppPackages, BundleArtifacts), `Directory.Build.props` (nullable, implicit usings, warnings as errors) and `Directory.Packages.props` (central package management); verify `git status` shows no `bin/`/`obj/` after a build
- [x] 1.2 Create `MordWare.slnx` with `src/MordWare.Core`, `src/MordWare.Persistence`, `src/MordWare.Integrations`, `src/MordWare.Vehicles` (all `net10.0`), with the project references from design D1; verify `dotnet build` succeeds
- [x] 1.3 Add the `Domain/`, `UseCases/` and `Data/` namespaces to `MordWare.Vehicles`, one marker type each (design D2); verify the build succeeds
- [x] 1.4 Create `src/MordWare.App` from the WinUI Blank App template (packaged, `net10.0-windows10.0.26100.0`, min `10.0.22621.0`), add CommunityToolkit.Mvvm, reference Core/Vehicles/Persistence/Integrations; verify `dotnet build -c Debug` succeeds

## 2. Editions and data paths

- [x] 2.1 Add `AppEnvironment` and the package-name resolver to `MordWare.Core` (design D3), with unit tests in `tests/MordWare.Tests.Unit` covering `MordWare` -> Prod, `MordWare.Dev` -> Dev, unknown or missing -> Dev; verify with `dotnet test --filter AppEnvironment`
- [x] 2.2 Add `IAppPaths` to Core, plus the `TempAppPaths` test helper (unique temp folder per run, deleted on dispose) with a unit test proving it never resolves under `%LOCALAPPDATA%\Packages`; verify with `dotnet test --filter AppPaths`
- [x] 2.3 Implement `PackagedAppPaths` in the App on `ApplicationData.Current.LocalFolder` and wire `IAppPaths`/`AppEnvironment` at startup; verify by logging nothing but reading the resolved folder in the debugger on a DEV run
- [x] 2.4 Add `Package.Dev.appxmanifest` (identity `MordWare.Dev`, display "mordWare (DEV)", DEV logo set) next to the PROD `Package.appxmanifest` (identity `MordWare`, display "mordWare"), with the configuration-based selection (design D4); verify a Debug build's generated `AppxManifest.xml` has name `MordWare.Dev` and a Release build's has `MordWare`
- [x] 2.5 Create the placeholder logo sets (PROD plus DEV with the "DEV" band) for every manifest asset size; verify the packaging build reports no missing assets

## 3. Window, DEV marker, localization

- [x] 3.1 Add `Strings/en-US/Resources.resw` and `Strings/it-IT/Resources.resw` with the window title and the DEV marker; set `PrimaryLanguageOverride` to `en-US` at startup (design D6); verify by a DEV run showing the English title
- [x] 3.2 Show the DEV marker in the main window only when `AppEnvironment` is Dev (MVVM: a ViewModel property, no code-behind logic); verify a DEV run shows it, and verify Italian by temporarily setting the override to `it-IT` (then revert)
- [x] 3.3 Manual side-by-side check: deploy DEV (F5) and install a locally built, self-signed PROD package; verify both appear in the Start menu under their own names, the PROD window has no marker, the two `LocalState` folders differ, and uninstalling DEV leaves PROD and its folder intact. Record the result in the task notes
  - Result (2026-11-04): PASS. PROD was registered from the Release build as a loose layout (`dotnet run -c Release`, Developer Mode), not as a self-signed package, so the machine certificate store stayed untouched; the identity behavior is the same. Start menu: "mordWare" (`MordWare_<hash>!App`) and "mordWare (DEV)" (`MordWare.Dev_<hash>!App`). PROD window: title "mordWare", plain icon, no marker. DEV window: "mordWare (DEV)", DEV icon, orange DEV badge; in Italian "mordWare (SVILUPPO)" with "SVIL". LocalState folders: `MordWare_<hash>` vs `MordWare.Dev_<hash>`. After `Remove-AppxPackage` of DEV, a file written into PROD's `LocalState\Data` was intact; DEV's whole package folder was gone (confirms the uninstall-deletes-LocalState risk for the MSIX storage spike). Test PROD registration removed afterwards; DEV redeployed.

## 4. Architecture tests

- [x] 4.1 Create `tests/MordWare.Tests.Architecture` with ArchUnitNET, loading every `MordWare.*` assembly including App (fall back to load-by-path or NetArchTest per design D7, and note it in design.md if used); verify the project builds
- [x] 4.2 Implement rules 1-5 from design D7 with the module list as a single constant; verify `dotnet test --filter Architecture` passes on the clean skeleton
- [x] 4.3 Prove each rule can fail: temporarily add a forbidden reference (Vehicles -> App, a Domain type using an EF Core attribute, a test using `PackagedAppPaths`, Core -> Vehicles), confirm the matching test fails with the offending dependency named, then remove it; record the outcome in the task notes
  - Result (2026-11-04): PASS, 4 of 4 rules failed with the offending dependency named, then all temporary code was removed and the clean run passed (5/5). Two literal probes are impossible by construction and were replaced by equivalents for the same rule. Vehicles -> App cannot compile (net10.0 vs Windows TFM), so rule 2 was proven with a second module: Integrations was temporarily listed as a module and Vehicles used one of its types ("TempCrossModule does depend on MordWare.Integrations.TempProbe"). Core -> Vehicles would be a project cycle, so rule 1 was proven with Core -> Persistence after temporarily dropping Persistence -> Core ("TempCoreUse does depend on MordWare.Persistence.TempPersist"). Rule 3: a Domain type using a type in an `Microsoft.EntityFrameworkCore.*` namespace failed. Rule 4: a unit test using a type named `MordWare.App.Hosting.PackagedAppPaths` failed.
  - Finding: ArchUnitNET's `ResideInAssembly(string)` matches only the full assembly name, so a short name silently selects nothing; the rules use `ResideInAssemblyMatching` with an exact-name regex, and every rule except rule 5 (empty Persistence/Integrations skeletons) requires at least one matching type, so a rule that selects nothing fails instead of passing.

## 5. CI

- [x] 5.1 Add `.github/workflows/ci.yml` (design D8: push to main + PRs, `windows-latest`, `timeout-minutes: 20`, concurrency cancel, NuGet cache, Release build without MSIX generation, `dotnet test --no-build`); verify locally that the same build and test commands succeed, and that `timeout-minutes` is set on every job

## 6. Project rules and docs

- [x] 6.1 Write `CLAUDE.md` (identity, numbered guardrails 1-10, definition-of-done table, workflow with optional branches/PRs, architecture-notes index) per design D9; verify every guardrail named in proposal.md exists
- [x] 6.2 Replace `openspec/config.yaml` with the adapted context pointer, per-artifact rules and operations guidance (design D9); verify `openspec validate bootstrap-solution` still passes
- [x] 6.3 Write `docs/architecture.md` (design D1-D7 in prose, the stale-registration fix for packaged F5) and `README.md` (what mordWare is, how to build and run DEV); verify the documented build and run commands work as written
- [x] 6.4 Write `docs/roadmap.md`: [0] bootstrap (this change), the three spikes (encryption library: SQLitePCLRaw SQLCipher status vs. SQLite3 Multiple Ciphers; decimal as fixed-scale integers km x100 / liters x1000 / money x10^4 with converters and in-memory stats; MSIX storage/uninstall: LocalState deleted on uninstall, backups outside the package, live DB never in OneDrive), [1] foundation (incl. DB environment marker, per-edition keys and backup folders, PROD->DEV restore allowed / DEV->PROD refused, WAL, no compaction), [2] morVehicles (garage -> refuel -> stats -> maintenance, standalone refuels with amount and currency, IExpenseBooking defined but not implemented), [3] importer (owner-only tool, DEV first then PROD), then morHouses -> morPantry -> morFinance, plus later items (/ship adaptation, GitHub repo + LICENSE, phone inbox spike, charting, UI verification, in-app guide); mark [0] in progress; verify each item has status, change, notes and references
- [x] 6.5 Add `CHANGELOG.md` with `## [Unreleased]` and an "Added" entry for the installable DEV/PROD app shell; verify the section exists
- [x] 6.6 (Added during apply at the owner's request.) Add `scripts/run-dev.ps1`, a PowerShell script that builds and launches the DEV edition only. It checks Developer Mode and the .NET SDK, never builds Release or touches the PROD package, offers `-NoBuild` (launch the already-deployed DEV) and `-Reset` (remove the DEV registration and its data first, after confirmation), and runs on Windows PowerShell 5.1 and PowerShell 7. Document it in `README.md` and `docs/architecture.md`; verify by running it (build + launch, then `-NoBuild`), confirming the "mordWare (DEV)" window opens, and closing it

## 7. Integration check

- [x] 7.1 Run `dotnet build` and `dotnet test` for the whole (still tiny) solution and `openspec validate bootstrap-solution --strict`; verify all three succeed

## Workflow follow-up

- Run /opsx:archive: merge the delta specs into `openspec/specs/`, mark [0] bootstrap done in `docs/roadmap.md`, then make one Conventional Commit on `main` (branches and PRs are optional in this phase).
- Creating the GitHub repository and pushing are separate decisions for the owner.
