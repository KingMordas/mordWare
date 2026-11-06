# Architecture

The solution layout, the module rules and how the architecture tests enforce them, the two editions (DEV/PROD) and how their data stays apart, localization, packing and releases, CI, the public-safety checks, and how to run the app. Read this before designing any change.

## Solution layout

```
MordWare.slnx
global.json                  pins the .NET 10 SDK; selects Microsoft.Testing.Platform for dotnet test
Directory.Build.props        nullable, implicit usings, warnings as errors; deterministic, path-free src/ builds
Directory.Packages.props     central package management: one version per dependency
.config/dotnet-tools.json    pins the vpk tool (Velopack's packer) for scripts/pack.ps1
LICENSE                      GPL-3.0 (unmodified text)
LICENSE-EXCEPTION.txt        the GPL-3.0 section 7 additional permission (Microsoft platform runtime)
THIRD-PARTY-NOTICES.txt      every redistributed component with its license
src/
  MordWare.Core              net10.0  shared contracts (editions, paths, languages, ...)
  MordWare.Persistence       net10.0  shared persistence infrastructure (empty until foundation)
  MordWare.Integrations      net10.0  external integrations (empty)
  MordWare.Vehicles          net10.0  the morVehicles module (skeleton)
  MordWare.App               net10.0-windows10.0.26100.0, min 10.0.22621.0 (Windows 11), WinUI 3, unpackaged, x64
tests/
  MordWare.Tests.Unit          net10.0  unit tests (xUnit v3)
  MordWare.Tests.Architecture  net10.0  module-boundary rules (ArchUnitNET), edition and notices checks
spikes/                      throwaway proofs for roadmap spikes; outside the solution (see below)
  Directory.Packages.props   imports the root file and adds spike-only package versions
  S1.Encryption              net10.0  encryption spike (xUnit v3)
  S2.Decimals                net10.0  decimal-storage spike (xUnit v3)
  S3.Storage                 WinUI 3  MSIX storage spike: packaged probe app + run-checks.ps1 driver
scripts/
  run-dev.ps1                builds and launches the DEV edition from its build output
  pack.ps1                   packs one edition into a Velopack installer, then scans it
  release-prep.ps1           the release run's checks (tag, main, changelog) and its release notes
  check-public-safety.ps1    the public-safety check (hooks, CI, manual, packages)
.githooks/                   tracked git hooks: pre-commit and commit-msg run the public-safety check
.github/workflows/           ci.yml (build, test, public safety) and release.yml (pack and publish)
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
- **Package versions.** `spikes/Directory.Packages.props` imports the root `Directory.Packages.props` and adds the spikes' own versions. The app's central versions change only when an item adopts a package on purpose. Guardrail 1 still applies to every spike dependency. S3 keeps the packaged-app packages (the `Microsoft.WindowsAppSDK` metapackage and `Microsoft.Windows.SDK.BuildTools.WinApp`) as spike-only versions, since the app no longer uses them.
- **Shape.** A spike is normally an xUnit v3 test project, so every success criterion is an assertion rather than printed output. Run it with `dotnet test --project spikes/<Spike>`.
  - **Exception: `S3.Storage`.** Package storage, write redirection and uninstall exist only for a process with package identity, which a test host lacks.
    - So S3 is a packaged WinUI 3 probe app under its own identity, `MordWare.Spike.S3`, never `MordWare` or `MordWare.Dev`. It is launched through the `s3probe.exe` alias.
    - Its driver `spikes/S3.Storage/run-checks.ps1` installs, resets and uninstalls it, and prints PASS or FAIL per check.
    - It runs on the owner's PC only (Developer Mode), never in CI. `run-checks.ps1 -CleanupOnly` removes the package and every spike folder.
- **Data.** Spike tests write only to their own temporary folders, deleted afterwards. They never use `IAppPaths` or either edition's data folder. S3's probe writes only into its own package's storage and into `mordWare S3 spike` folders under Documents and `%LOCALAPPDATA%`, all deleted by its cleanup.
- **Lifetime.** A spike's outcome is recorded in `docs/roadmap.md`. Its code stays as a reference until the roadmap item that adopts it moves the code into `src/`, and that item deletes the spike folder (as [D] did for [S4]).

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
4. Test assemblies never depend on `MordWare.App.Hosting.LocalAppDataPaths` or `AppHost`, so tests can never resolve a real edition's data folder.
5. Persistence and Integrations depend on no module and not on the App.

The same project also checks two delivery facts:
- **Edition per configuration** (`EditionPerConfigurationTests`): the App built in the test run's configuration carries `[assembly: AssemblyMetadata("MordWare.Edition", ...)]` = `DEV` for every configuration except Release, which is `PROD`. It is read from the build output with `System.Reflection.Metadata`, never loaded. CI runs Release, a local `dotnet test` runs Debug.
- **Third-party notices** (`ThirdPartyNoticesTests`): every App `PackageReference`, every known transitive redistributable (the .NET runtime pack, the Windows SDK projection, WebView2) and every package the App's restore resolves (`obj/project.assets.json`, build-time-only packages excepted) has a `Package: <id>` entry in `THIRD-PARTY-NOTICES.txt`. A new dependency cannot ship unlisted.

How the tests are built and kept honest:
- **Module list.** The modules live in `MordWareArchitecture.Modules`. Adding a module means adding its name there (definition of done).
- **Loading.** The App and the unit-test project are build-order-only references (`ReferenceOutputAssembly="false"`), and their DLLs are read by path from the same-configuration build output: `bin\<configuration>\` or, when the project is built for a platform (the App is always x64), `bin\<platform>\<configuration>\`. The newest match wins. ArchUnitNET reads metadata only (by path, never `Assembly.LoadFile`), so WinUI never loads in the test process.
- **Exact-name matching.** Rules select assemblies with `ResideInAssemblyMatching` and an exact-name regex. `ResideInAssembly(string)` only matches the *full* assembly name and would silently select nothing.
- **A rule that selects nothing fails.** Every rule must match at least one type, except rule 5 while Persistence and Integrations are empty skeletons.

## Editions: DEV and PROD

The owner develops on the same PC that runs the real app, so the separation is structural and fixed by the build:

| | PROD | DEV |
|---|---|---|
| Built by | `Release` only | every other configuration (`Debug`, F5, tests) |
| Compile constant | `MORDWARE_PROD` | none |
| Assembly metadata `MordWare.Edition` | `PROD` | `DEV` |
| Display name | mordWare | mordWare (DEV) / mordWare (SVILUPPO) |
| Icons | `Assets/` | `Assets/Dev/` (orange DEV band) |
| Window | no marker | orange DEV badge in the title bar |
| Runs from | the installer only | its build output (`scripts/run-dev.ps1`); a local check-only installer for installer checks |
| Installer packId and title | `MordWare`, "mordWare" | `MordWare.Dev`, "mordWare (DEV)" (never published) |
| Program files (installed) | `%LOCALAPPDATA%\MordWare\` | `%LOCALAPPDATA%\MordWare.Dev\` |
| Data | `%LOCALAPPDATA%\mordWare Data\` | `%LOCALAPPDATA%\mordWare Data (DEV)\` |

- **The edition is a build constant.** The App defines `MORDWARE_PROD` only when `Configuration == Release`; `Hosting/AppEdition.Current` maps it to `AppEnvironment`. Nothing at run time (no setting, argument or environment value) can change it. The same condition stamps the `MordWare.Edition` assembly metadata, which the architecture tests check per configuration, and picks the exe icon.
- **Names come from Core.** `EditionProfile.For(AppEnvironment)` returns the packId, the installer title and the data folder name. It is pure and unit-tested. `scripts/pack.ps1` and `scripts/run-dev.ps1` keep their own copy of the names they need, each behind a guard (`pack.ps1 -SelfTest` also compares its table with `EditionProfile`).
- **Data paths.** Code asks `IAppPaths` (`DataFolder`, `LogsFolder`). The app uses `Hosting/LocalAppDataPaths`: `DataFolder` is the edition's data root itself, and `LogsFolder` is `<root>\Logs`; both are created on first use. Tests use the `TempAppPaths` helper, which creates a unique temp folder and deletes it on dispose; architecture rule 4 keeps them away from the real class.
- **Why the data root is a sibling of the program files.** Velopack deletes `%LOCALAPPDATA%\<packId>\` on uninstall and on a Setup over an existing install ([S4]); the data roots are never inside it. The two data folder names differ by more than case (Windows paths ignore case), and neither contains the other.
- **DEV is never published.** Only the release workflow packs, and it packs PROD only. A DEV installer exists only as a local, check-only build (`pack.ps1 -Edition Dev`) for installer and upgrade checks; those checks never install, run or touch PROD or `mordWare Data`.
- **Before installing PROD on a PC, `%LOCALAPPDATA%\MordWare\` must not hold anything else:** Velopack owns that folder and deletes it on uninstall.
- **Backups live in a user-chosen folder outside the program files and outside AppData.**
  - The default is `Documents\mordWare Backups` (PROD) or `Documents\mordWare Backups (DEV)`, so the editions never share a backup folder.
  - Every backup records its edition, and a DEV backup is refused by PROD.
  - Documents may be redirected into OneDrive; that is accepted, because backups are encrypted and written whole (a `.tmp` file, then a rename). The live database is never in a synced folder.
  - Foundation builds this; the backup folder is added to `IAppPaths` there.

## Localization

- **Resources.** User-facing strings live in `src/MordWare.App/Strings/en-US/Resources.resw` and `Strings/it-IT/Resources.resw`. XAML uses `x:Uid`; code uses `ResourceLoader`.
- **Language at startup.** `Program.Main` calls `App.ApplyAppLanguage` before the XAML application starts. It sets the Windows App SDK's `Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride` and the .NET current cultures to the app language on every start. The `Windows.Globalization` override needs package identity and throws in the unpackaged app; set any later (for example in `OnLaunched`), the override no longer changes the language resources load in.
- **Default language.** Until the foundation change adds a language setting, the language is `AppLanguages.Default` (en-US).

## Running and debugging

- **Build everything:** `dotnet build MordWare.slnx` (Debug = DEV). The App always builds for x64 (`bin\x64\<configuration>\...\win-x64\`).
- **Run DEV:** `.\scripts\run-dev.ps1` (Windows PowerShell 5.1 or PowerShell 7), or F5 in Visual Studio.
  - The script builds the App in Debug for x64, then starts `MordWare.App.exe` from the build output, independent of the console.
  - `-NoBuild` starts the existing build.
  - `-Reset` deletes the `mordWare Data (DEV)` folder first, after confirmation (`-Force` skips the prompt). A guard refuses any folder name that does not end in `(DEV)`.
  - If a `MordWare.Dev` app registration from the earlier MSIX setup is still on the account, it offers once to remove it (DEV only, exact name; a "no" is remembered in the DEV data folder).
  - It never builds Release, and never touches `%LOCALAPPDATA%\MordWare\` or `mordWare Data`.
- **Entry point.** `Program.Main` (the project defines `DISABLE_XAML_GENERATED_MAIN`) runs Velopack's install hooks first (`VelopackApp.Build().Run()`, which exits when the installer started the app for a hook), then applies the language and starts WinUI. No logger is attached to Velopack: its messages contain user-profile paths (guardrail 6).
- **Why `EnableMsixTooling` stays `true`** although the app is unpackaged: without it, `dotnet publish` drops the app's own `.pri` (resources and compiled XAML) and the installed window crashes ([S4] (j)).
- **Tests:**
  - Whole suite: `dotnet test --solution MordWare.slnx`.
  - Focused: `dotnet test --project tests/MordWare.Tests.Unit --filter-class "*EditionProfile*"`.
  - Architecture tests read the App's build output, and `dotnet test` builds it first through the build-order reference.
- **Never test against PROD.** Running or installing PROD for a check is done only when a task explicitly requires it, with the owner's approval, and anything it created is removed afterwards.

## Packing

`scripts/pack.ps1 -Edition Prod|Dev -Version <semver> [-OutputDir <path>]` (PowerShell 7) turns one edition into a per-user Velopack installer:

1. `dotnet publish` of the App, self-contained for `win-x64`: Release for Prod, Debug for Dev, with `-p:Version=<version>`, `ContinuousIntegrationBuild` and no PDBs.
2. The legal texts go into the publish folder: `LICENSE`, `LICENSE-EXCEPTION.txt` and `THIRD-PARTY-NOTICES.txt` from the repository, plus the upstream notices of the Windows App SDK and the .NET runtime, copied byte for byte from their NuGet packages into `third-party-notices\<package>\`. The script refuses to pack without any of them.
3. `dotnet vpk pack` (vpk pinned in `.config/dotnet-tools.json`) with the edition's packId, title and icon, the default channel `win`, a Start menu shortcut only (`--shortcuts StartMenuRoot`), no portable zip (`--noPortable`) and no delta package (`--delta None`).
4. The package scan, `check-public-safety.ps1 -Package` over the Velopack output and the publish folder (see [Public-safety checks](#public-safety-checks)).

- **Output** (gitignored): `artifacts\pack\<Edition>\publish` (cleaned every run) and `artifacts\pack\<Edition>\releases`, which keeps earlier versions. `Setup.exe` is named per packId, so copy it aside before packing the next version for an upgrade check.
- **Versions:** a release uses the tag's version; the dry run `0.0.0-dryrun.<run number>`; local checks `0.0.<n>`. The App's own default is `0.0.0-dev`.
- **The name guard.** `pack.ps1` refuses any edition, packId, title and configuration that is not exactly PROD = `MordWare` / "mordWare" / Release or DEV = `MordWare.Dev` / "mordWare (DEV)" / Debug. `pack.ps1 -SelfTest` proves the guard and compares the names with `EditionProfile`.
- **Only Prod is published**, and only by the release workflow. `pack.ps1 -Edition Dev` is the check-only DEV installer: install, upgrade and uninstall checks use it, never a Prod `Setup.exe`.
- **Setup over an existing install:** Velopack's `Setup.exe` fails when started within about 2 seconds of the previous install or app run ("Failed to remove existing application directory"). Wait a few seconds before installing a newer version over an older one.
- **Deterministic, path-free binaries.** `Directory.Build.props` sets `Deterministic`, `PathMap` (repository root to `/_/`) and, in Release, `DebugType=none` for every project under `src/`; `ContinuousIntegrationBuild` is set by CI and by `pack.ps1`. Local Debug builds keep their PDBs.
- **Trimmed Windows App SDK.** The App references only the component packages it needs (`Microsoft.WindowsAppSDK.Base`, `.Foundation`, `.InteractiveExperiences`, `.WinUI`, `.DWrite`, all 2.6.5), never the `Microsoft.WindowsAppSDK` metapackage: no Windows ML, AI, Search or Widgets runtime ships (no `onnxruntime*`, `DirectML*` or `Microsoft.Windows.AI*` file). A component added later goes into `Directory.Packages.props`, `LICENSE-EXCEPTION.txt` and `THIRD-PARTY-NOTICES.txt` in the same change.

## Releases

`.github/workflows/release.yml`:

- **Triggers.** A pushed tag `v*.*.*` (a release), and `workflow_dispatch` (the dry run). Release runs are never cancelled by a newer run.
- **Job `release`** (`windows-latest`, 30 minutes, `contents: read`):
  1. Full checkout.
  2. `scripts/release-prep.ps1`: for a tag, the tag must be `vX.Y.Z`, the tagged commit must be on `main` (`git merge-base --is-ancestor <sha> origin/main`), and `CHANGELOG.md` must have a `## [X.Y.Z]` section; the version is `X.Y.Z`. For the dry run, the version is `0.0.0-dryrun.<run number>` and the notes use `## [Unreleased]`. In both modes it writes the release notes: the changelog section, then "mordWare is licensed GPL-3.0-only with an additional permission (LICENSE-EXCEPTION.txt). Source code for this version: <repository>/tree/<tag or commit>".
  3. Restore, Release build and the full test suite. A failing test stops the job before anything is packed.
  4. `pack.ps1 -Edition Prod -Version <version>`, which includes the package scan. DEV is never packed here.
  5. The Velopack output and the notes file are uploaded as a workflow artifact (`mordWare-<version>`, kept 7 days).
- **Job `publish`**, only for a pushed tag (never for `workflow_dispatch`, so the dry run cannot publish), `contents: write`: downloads the artifact, runs `vpk upload github --publish --tag vX.Y.Z --releaseName "mordWare X.Y.Z" --targetCommitish <sha>` with the token in `VPK_TOKEN` (never on the command line), then sets the release notes with `gh release edit --notes-file`. GitHub adds the tag's source archives to the release.
- **Testing the workflow.** There is no release before v1.0.0: the dry run is the release workflow's test. It does every step but `publish`, and its artifact shows the exact installer and notes a release would get. `release-prep.ps1 -SelfTest` checks the tag, main and changelog rules against a throwaway repository.
- **Delivery facts** ([S4]). The installer is per user, without admin rights, and installs into `%LOCALAPPDATA%\<packId>\` with a Start menu entry and an installed-apps entry. A newer installer upgrades in place; uninstalling removes the program files, the Start menu entry and the installed-apps entry, and leaves the data root. The app is x64 only and not code-signed: the browser and SmartScreen may warn on first run, and Smart App Control, where it is on, blocks unsigned apps (an owner decision before v1.0.0, see the roadmap).
- **Updates** come with roadmap item [U]: at startup the app checks GitHub Releases (on by default, can be turned off, sends no user data) and installs a newer version only after the user clicks "Update".

## CI

- **Workflow.** `.github/workflows/ci.yml` runs on push to `main` and on pull requests, and cancels superseded runs. Every job runs on `windows-latest` with a timeout. It never packs; packing belongs to `release.yml`.
- **`build-test` job** (`timeout-minutes: 20`). Restore, Release build, then `dotnet test --solution MordWare.slnx -c Release --no-build`.
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
| Package scan | `pack.ps1`, locally and in `release.yml` before any upload | `-Package` over the packed installer (see below). |
| Claude Code hook (`.claude/settings.json`) | every shell command the assistant runs | Blocks `--no-verify` / `commit -n`, `core.hooksPath` changes and repository visibility changes. |
| GitHub settings | owner's account and repository | E-mail privacy, blocking pushes that expose a private e-mail, secret scanning and push protection. |

- **Modes of the script:** `-Staged` (the pre-commit hook), `-MessageFile <path>` (the commit-msg hook), `-All` (every tracked file: CI and before going public), `-History` (every commit's author and committer identity, plus gitleaks over the full history), `-Package <folder>...` (a packed release) and `-SelfTest` (builds a throwaway repository and a throwaway package in a temp folder and proves every rule refuses its sample).
- **The package scan (`-Package`).** Every `.nupkg` is expanded, and each `Setup.exe` must embed its full package byte for byte. Strings are read as ASCII and as UTF-16 (both byte offsets), split into printable runs.
  - **Own files** (`MordWare.*`, `Assets`, the three legal texts, the package metadata) must pass the user-path, this-PC (profile path, user name, computer name), package-hash, e-mail, private-key and private-word rules.
  - **Third-party files** pass only if byte-identical (SHA-256) to a file of their upstream package in the NuGet global packages folder or the .NET packs folder; the copies in `third-party-notices\` are such files.
  - **Velopack-derived binaries**, the one exception: vpk builds the `Setup.exe` stub, the shortcut stub (`*_ExecutionStub.exe`) and `Squirrel.exe` per app (manifest, icon and version resources), so they can never be identical to upstream. Each passes only if every rule hit in it also occurs, with the same text, in its vendor binary of the pinned vpk package (`setup*.exe`, `stub*.exe`, `update*.exe`), and it has no this-PC or private-word hit.
  - The report lists the accepted third-party files per upstream package.
- **No bypass.** There is no switch to skip a rule. A false positive is fixed by changing the content (for example, a placeholder such as `<user folder>`) or, for a rule that is genuinely wrong, by changing the script in its own reviewed change.
