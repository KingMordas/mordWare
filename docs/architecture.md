# Architecture

The solution layout, the module rules and how the architecture tests enforce them, the two editions (DEV/PROD) and how their data stays apart, localization, delivery, CI, the public-safety checks, and how to run the app. Read this before designing any change.

> **Packaging is changing.** The sections on editions, data paths and running DEV describe today's code, which is packaged as MSIX. Roadmap item [D] makes mordWare an unpackaged app delivered by an installer (see [Delivery](#delivery-planned)); those sections are rewritten in that change.

## Solution layout

```
MordWare.slnx
global.json                  pins the .NET 10 SDK; selects Microsoft.Testing.Platform for dotnet test
Directory.Build.props        nullable, implicit usings, warnings as errors
Directory.Packages.props     central package management: one version per dependency
src/
  MordWare.Core              net10.0  shared contracts (editions, paths, languages, ...)
  MordWare.Persistence       net10.0  shared persistence infrastructure (empty until foundation)
  MordWare.Integrations      net10.0  external integrations (empty)
  MordWare.Vehicles          net10.0  the morVehicles module (skeleton)
  MordWare.App               net10.0-windows10.0.26100.0, min 10.0.22621.0 (Windows 11), WinUI 3, packaged MSIX
tests/
  MordWare.Tests.Unit          net10.0  unit tests (xUnit v3)
  MordWare.Tests.Architecture  net10.0  module-boundary rules (ArchUnitNET)
spikes/                      throwaway proofs for roadmap spikes; outside the solution (see below)
  Directory.Packages.props   imports the root file and adds spike-only package versions
  S1.Encryption              net10.0  encryption spike (xUnit v3)
  S2.Decimals                net10.0  decimal-storage spike (xUnit v3)
  S3.Storage                 WinUI 3  MSIX storage spike: packaged probe app + run-checks.ps1 driver
scripts/
  run-dev.ps1                builds, deploys and launches the DEV edition
  check-public-safety.ps1    the public-safety check (hooks, CI, manual)
.githooks/                   tracked git hooks: pre-commit and commit-msg run the public-safety check
.claude/                     OpenSpec commands, and a Claude Code hook that blocks hook bypass and visibility changes
```

The libraries target plain `net10.0`, not a Windows TFM, so WinUI types are unavailable to them at compile time. That is the first layer of the "no UI in domain" rule; the architecture tests are the second.

## Spikes

`spikes/` holds the throwaway code that roadmap spikes (S1, S2, ...) use to prove a decision before a real item builds on it.

- **Kept outside the product.**
  - Not in `MordWare.slnx`, so the app build and CI never compile it.
  - Never named `MordWare.*`, so the architecture tests never load it.
  - Referenced by nothing in `src/` or `tests/`.
  - Independent of each other: a spike copies the small helpers it needs (encrypted connection, temp folder) instead of referencing another spike, so each can be deleted on its own.
- **Package versions.** `spikes/Directory.Packages.props` imports the root `Directory.Packages.props` and adds the spikes' own versions. The app's central versions change only when an item adopts a package on purpose. Guardrail 1 still applies to every spike dependency.
- **Shape.** A spike is normally an xUnit v3 test project, so every success criterion is an assertion rather than printed output. Run it with `dotnet test --project spikes/<Spike>`.
  - **Exception: `S3.Storage`.** Package storage, write redirection and uninstall exist only for a process with package identity, which a test host lacks.
    - So S3 is a packaged WinUI 3 probe app under its own identity, `MordWare.Spike.S3`, never `MordWare` or `MordWare.Dev`. It is launched through the `s3probe.exe` alias.
    - Its driver `spikes/S3.Storage/run-checks.ps1` installs, resets and uninstalls it, and prints PASS or FAIL per check.
    - It runs on the owner's PC only (Developer Mode), never in CI. `run-checks.ps1 -CleanupOnly` removes the package and every spike folder.
  - **Exception: `S4.Installer`.** Installing, updating and uninstalling an unpackaged app need a real installer and a real process, which a test host lacks.
    - So S4 is an unpackaged WinUI 3 probe app with Velopack, packed by `vpk` (pinned in the spike's own `.config/dotnet-tools.json`) under its own packIds, `MordWare.Spike.S4` (Release, PROD-like) and `MordWare.Spike.S4.Dev` (Debug, DEV-like), never `MordWare` or `MordWare.Dev`.
    - Its driver `spikes/S4.Installer/run-checks.ps1` packs, installs, upgrades, updates and uninstalls it per user, and prints PASS or FAIL per check. A name guard refuses any packId or folder that is not a spike name.
    - It runs on the owner's PC only, without admin rights and without changing any Windows setting, never in CI. `run-checks.ps1 -CleanupOnly` removes every spike install, data root, feed and publish folder.
- **Data.** Spike tests write only to their own temporary folders, deleted afterwards. They never use `IAppPaths` or either edition's LocalState. S3's probe writes only into its own package's storage and into `mordWare S3 spike` folders under Documents and `%LOCALAPPDATA%`, all deleted by its cleanup. S4's probe writes only into `mordWare S4 spike Data` and `mordWare S4 spike Data (DEV)` under `%LOCALAPPDATA%`, and its driver uses `mordWare S4 spike feed` and `mordWare S4 spike publish` under `%TEMP%`, all deleted by its cleanup.
- **Lifetime.** A spike's outcome is recorded in `docs/roadmap.md`. Its code stays as a reference until the roadmap item that adopts it moves the code into `src/`, and that item deletes the spike folder.

## Modules

Dependency direction: `App -> modules -> Persistence/Integrations -> Core`, and `App -> Core`.

- **One project per module** (`MordWare.<Module>`), with three namespaces:
  - `Domain`: pure C#, with no EF Core and no WinUI; unit-tested without a database.
  - `UseCases`: the application logic the UI calls.
  - `Data`: the module's own DbContext and mappings, from foundation onward. All module contexts share one SQLite file, each with its own migrations history table.
- **Cross-module references are plain ids**, with no database foreign keys.
- **A cross-module feature goes through a contract owned by the providing module.** For example, Vehicles books a cost through Finance's `IExpenseBooking`. When the provider is absent, the feature is simply unavailable.
- **Persistence and Integrations are shared infrastructure.** Modules may use them; they never depend on a module.

## Architecture tests

`tests/MordWare.Tests.Architecture` (ArchUnitNET, Apache-2.0) enforces five rules:

1. `MordWare.Core` depends on no other `MordWare.*` assembly.
2. A module depends only on Core, Persistence and Integrations: never on the App, never on another module.
3. Any `MordWare.<X>.Domain` namespace depends on neither `Microsoft.EntityFrameworkCore*` nor `Microsoft.UI*`/`Windows*`.
4. Test assemblies never depend on `MordWare.App.Hosting.PackagedAppPaths` or `AppHost`, so tests can never resolve a real edition's data folder.
5. Persistence and Integrations depend on no module and not on the App.

How the tests are built and kept honest:
- **Module list.** The modules live in `MordWareArchitecture.Modules`. Adding a module means adding its name there (definition of done).
- **Loading.** The App and the unit-test project are build-order-only references (`ReferenceOutputAssembly="false"`), and their DLLs are read by path from the same-configuration build output: `bin\<configuration>\` or, when the solution builds the project for a platform (the App is mapped to x86), `bin\<platform>\<configuration>\`. The newest match wins and the AppX layout is never read. ArchUnitNET reads metadata only (by path, never `Assembly.LoadFile`), so WinUI never loads in the test process, and a Release App built for x86 with ReadyToRun is read by a 64-bit test process too.
- **Exact-name matching.** Rules select assemblies with `ResideInAssemblyMatching` and an exact-name regex. `ResideInAssembly(string)` only matches the *full* assembly name and would silently select nothing.
- **A rule that selects nothing fails.** Every rule must match at least one type, except rule 5 while Persistence and Integrations are empty skeletons.

## Editions: DEV and PROD

_Current until [D]: the mechanism below is the MSIX package identity. The rule (the build fixes the edition; the data never mixes) stays._

The owner develops on the same PC that runs the real app, so the separation is structural:

| | PROD | DEV |
|---|---|---|
| Package identity | `MordWare` | `MordWare.Dev` |
| Display name | mordWare | mordWare (DEV) / mordWare (SVILUPPO) |
| Built by | `Release` only | every other configuration (`Debug`, F5, `dotnet run`) |
| Manifest | `Package.appxmanifest` | `Package.Dev.appxmanifest` |
| Logos | `Assets/` | `Assets/Dev/` (orange DEV band) |
| Window | no marker | orange DEV badge in the title bar |
| Data | `%LOCALAPPDATA%\Packages\MordWare_<hash>\LocalState` | `%LOCALAPPDATA%\Packages\MordWare.Dev_<hash>\LocalState` |

- **The edition comes from the running package's identity name** (`AppEnvironmentResolver.FromPackageName`), never from a compile flag. Windows uses the same identity to separate each package's storage, so the DEV marker and the data folder can't disagree. Anything other than the exact PROD name, including no package identity at all, resolves to DEV.
- **Data paths.** Code asks `IAppPaths` (`DataFolder`, `LogsFolder`). The installed app uses `PackagedAppPaths` (package LocalState). Tests use the `TempAppPaths` helper, which creates a unique temp folder and deletes it on dispose.
- **What Windows does to package storage** (measured in [S3], `docs/roadmap.md`):
  - **Uninstall and Settings > Apps > Reset delete LocalState.** Reset is refused on a development-mode registration, so DEV deployed by `run-dev.ps1` cannot be reset; use `run-dev.ps1 -Reset` (an uninstall) instead.
  - **A redeploy or a package update keeps LocalState.** That includes `run-dev.ps1` re-registering DEV at the same version.
  - **Writes under `%LOCALAPPDATA%` / `%APPDATA%` are unreliable.** A packaged app's write into a new folder there is redirected into `Packages\<family>\LocalCache\` and deleted on uninstall, while a write into a folder that already existed goes to the real path. Nothing durable goes under AppData.
- **Backups live in a user-chosen folder outside the package and outside AppData.**
  - The default is `Documents\mordWare Backups` (PROD) or `Documents\mordWare Backups (DEV)`, so the editions never share a backup folder.
  - Every backup records its edition, and a DEV backup is refused by PROD.
  - Documents may be redirected into OneDrive; that is accepted, because backups are encrypted and written whole (a `.tmp` file, then a rename). The live database is never in a synced folder.
  - Foundation builds this; the backup folder is added to `IAppPaths` there.
- **Verified side by side (2026-11-04).** Both editions installed together, each with its own Start menu entry and its own LocalState. Uninstalling DEV left PROD and its data intact.

## Localization

- **Resources.** User-facing strings live in `src/MordWare.App/Strings/en-US/Resources.resw` and `Strings/it-IT/Resources.resw`. XAML uses `x:Uid`; code uses `ResourceLoader`.
- **Language at startup.** `App.ApplyAppLanguage` sets `ApplicationLanguages.PrimaryLanguageOverride` and the .NET current cultures to the app language on every start. Windows persists the override per package, so it must be set every time.
- **Default language.** Until the foundation change adds a language setting, the language is `AppLanguages.Default` (en-US).

## Running and debugging

_Current until [D], which removes the package registration and Developer Mode from the DEV workflow._

- **Build everything:** `dotnet build MordWare.slnx`
- **Run DEV:** `.\scripts\run-dev.ps1` (Windows PowerShell 5.1 or PowerShell 7), or F5 in Visual Studio.
  - The script checks Developer Mode and the SDK, then registers the `MordWare.Dev` package from the Debug build output and starts it through its Start menu identity (`shell:AppsFolder\MordWare.Dev_<hash>!App`).
  - `-NoBuild` starts the already-deployed DEV edition.
  - `-Reset` removes the DEV registration and its data first, after confirmation (`-Force` skips the prompt).
  - It never builds Release and never touches the `MordWare` (PROD) package.
- **Why the script doesn't let `dotnet run` launch the app.** `dotnet run` on this project (via `Microsoft.Windows.SDK.BuildTools.WinApp`) launches the app and then waits until its window closes. If that `dotnet run` is interrupted, the app can disappear with it. The script passes `-p:WinAppRunNoLaunch=true`, so `dotnet run` only builds and registers, and the app runs independently of the console. To use `dotnet run` directly without blocking, add `-p:WinAppRunDetach=true`.
- **Tests:**
  - Whole suite: `dotnet test --solution MordWare.slnx`.
  - Focused: `dotnet test --project tests/MordWare.Tests.Unit --filter-class "*AppEnvironment*"`.
  - Architecture tests read the App's build output, and `dotnet test` builds it first through the build-order reference.
- **Stale DEV registration** (deploy fails, or an old build keeps launching): remove the DEV package with `Get-AppxPackage MordWare.Dev | Remove-AppxPackage`, then run again. This deletes DEV's LocalState, and only DEV's.
- **Never test against PROD.** Running or installing the `MordWare` package for a check is done only when a task explicitly requires it, and the test registration is removed afterwards.

## Delivery (planned)

Decided 2026-11-05; built by roadmap items [S4], [D] and [U].

- **No Microsoft Store and no code signing.** An unsigned MSIX cannot be installed ([S3]), so mordWare becomes an **unpackaged** WinUI 3 app (Windows App SDK self-contained).
- **Installer.** A per-user installer that installs mordWare or upgrades an existing installation, without admin rights, published on GitHub Releases and linked from the project website. [S4] confirmed Velopack (MIT): its `Setup.exe` installs into `%LOCALAPPDATA%\<packId>\`, delta packages keep updates small, and its update API reads GitHub Releases or a local folder. The app is x64 only.
- **Unsigned first run.** The browser may warn about the download, and Windows SmartScreen shows "Windows protected your PC" for an unsigned installer ("More info", then "Run anyway"). Windows 11's Smart App Control, where it is on, blocks unsigned executables with no per-app exception; whether that changes the no-signing decision is an owner decision before v1.0.0 (roadmap [D]).
- **Editions.** Fixed by the build (Release = PROD, every other configuration = DEV), with separate app ids, data folders, Start menu entries and icons. The data folder never lives inside the installer's own folder, which Velopack deletes on uninstall and on a reinstall over it. Decided in [S4]: `%LOCALAPPDATA%\mordWare Data\` (PROD) and `%LOCALAPPDATA%\mordWare Data (DEV)\`, siblings of the install folder. [D] and Foundation build them.
- **Releases.** A release workflow runs when a `v*.*.*` tag that points at a commit on `main` is pushed: build, test, pack the installer, publish the GitHub Release. The version comes from the tag. A manual dry-run mode builds the installer without publishing. No release exists before v1.0.0.
- **Updates.** At startup the app checks GitHub Releases (on by default, can be turned off, sends no user data) and installs a newer version only after the user clicks "Update".

## CI

- **Workflow.** `.github/workflows/ci.yml` runs on push to `main` and on pull requests, and cancels superseded runs. Every job runs on `windows-latest` with a timeout.
- **`build-test` job** (`timeout-minutes: 20`). Restore with `-p:Configuration=Release` (the App enables ReadyToRun in Release, whose runtime pack must be restored for that configuration), Release build with `GenerateAppxPackageOnBuild=false`, then `dotnet test --solution MordWare.slnx -c Release --no-build`.
- **`public-safety` job** (`timeout-minutes: 10`). Full-history checkout, a pinned gitleaks release verified by SHA-256, then `scripts/check-public-safety.ps1 -History` (every commit's identity, and gitleaks over the whole history), `-All` and `-SelfTest`. Commits in a pull request must therefore use a GitHub no-reply address too.
- **Cost.** Standard GitHub-hosted runners are free for public repositories; the timeouts stay so a hung job cannot run for hours.
- **Active from the first push** to `KingMordas/mordWare` (roadmap [R]).

## Public-safety checks

The repository is public, so nothing personal, secret, machine-specific or about the earlier web app may ever be committed (guardrail 11). The checklist is [`public-safety.md`](public-safety.md); the layers that enforce it are:

| Layer | Where | What it checks |
|---|---|---|
| `scripts/check-public-safety.ps1` | called by every layer below | Author and committer identity, forbidden file types (keys, certificates, databases, backups), absolute user-folder paths, package-family hashes, e-mail addresses (except no-reply and example domains), secrets (gitleaks), and the owner's private words. |
| Git hooks (`.githooks/`) | every local commit, after `git config core.hooksPath .githooks` | `pre-commit` runs `-Staged`; `commit-msg` runs `-MessageFile`. |
| Private deny list | `.public-safety.local.txt`, gitignored | The owner's own words (real name, e-mail, local names). It is never committed, so the check never publishes what it looks for. CI cannot see it. |
| CI `public-safety` job | every push and pull request | `-History` (identities and gitleaks over the full history), `-All` and `-SelfTest`. |
| Claude Code hook (`.claude/settings.json`) | every shell command the assistant runs | Blocks `--no-verify` / `commit -n`, `core.hooksPath` changes and repository visibility changes. |
| GitHub settings | owner's account and repository | E-mail privacy, blocking pushes that expose a private e-mail, secret scanning and push protection. |

- **Modes of the script:** `-Staged` (the pre-commit hook), `-MessageFile <path>` (the commit-msg hook), `-All` (every tracked file: CI and before going public), `-History` (every commit's author and committer identity, plus gitleaks over the full history) and `-SelfTest` (builds a throwaway repository in a temp folder and proves every rule refuses its sample).
- **No bypass.** There is no switch to skip a rule. A false positive is fixed by changing the content (for example, a placeholder such as `<user folder>`) or, for a rule that is genuinely wrong, by changing the script in its own reviewed change.
