# Design

## Context

See `proposal.md` for the motivation. Current state:

- `MordWare.App` is a packaged WinUI 3 app. Two `Package*.appxmanifest` files are selected by configuration, `Microsoft.Windows.SDK.BuildTools.WinApp` registers it for `dotnet run`, and `MordWare.slnx` maps it to x86.
- `AppHost.Create` reads `Package.Current.Id.Name` and passes it to `AppEnvironmentResolver.FromPackageName` (Core). `PackagedAppPaths` uses `ApplicationData.Current.LocalFolder`. Both throw or degrade without package identity.
- `App.ApplyAppLanguage` uses `Windows.Globalization.ApplicationLanguages`, which throws unpackaged ([S4] (d)).
- `MainWindowViewModel` already chooses title, DEV marker and icon from `AppEnvironment`. That pattern stays.
- The architecture tests (rule 4) name `MordWare.App.Hosting.PackagedAppPaths`. `MordWareArchitecture.FindBuildOutput` skips `AppX` folders, and its docs say the App is mapped to x86.
- `spikes/S4.Installer` holds the proven reference: project properties, `Program.Main`, `run-checks.ps1` (vpk pack and install calls) and `scan-package.ps1` (binary scan).
- **No user data exists anywhere.** There is no persistence yet, and an unsigned PROD MSIX was never installable, so nothing has to be migrated from LocalState (guardrail 5 is not triggered).

## Goals / Non-Goals

**Goals:**
- Running from the build output, as an installed check-only DEV build and as installed PROD must all behave identically, apart from edition-specific names, icons and folders.
- The release workflow can be exercised end to end (dry run) without ever creating a release.
- Every guardrail exception introduced here is named, narrow and documented in the same change.

**Non-Goals:**
- No update check or update UI ([U]). Velopack is wired only for its install hooks.
- No settings file, About page or logging framework ([1]).
- No change to the module projects or `MordWare.Persistence`.

## Decisions

### D1. Unpackaged project, following the [S4] probe

`MordWare.App.csproj` adopts the [S4] probe's properties: `WindowsPackageType=None`, `WindowsAppSDKSelfContained=true`, `SelfContained=true`, `RuntimeIdentifier=win-x64`, `Platforms=x64`, and **`EnableMsixTooling=true`** (without it, publish drops the app's `.pri` and the installed window crashes, [S4] (j)).

Removed:
- the manifest item groups and both `Package*.appxmanifest` files;
- the `Msix` project capability and the "Package and Publish" menu blocks;
- `Microsoft.Windows.SDK.BuildTools.WinApp` (packaged `dotnet run` support only);
- the three publish profiles and the `PublishProfile`/`RuntimeIdentifier` auto-selection.

`PublishReadyToRun` is set to `false` in every configuration. The framework files are already precompiled, so the gain on our small assemblies is negligible, and off keeps the [S4] sizes and deltas. CI's "restore for Release because of ReadyToRun" comment goes away with it.

`MordWare.slnx` maps the App to x64. The ARM64/x86 platforms are dropped.

*Alternative considered:* keeping the MSIX for DEV only. Rejected: it keeps Developer Mode and a second runtime model alive, and DEV would no longer test the shipped (unpackaged) code paths.

### D2. Edition from a build constant, names from Core

- **The constant.** The App defines `MORDWARE_PROD` only when `Configuration == Release` (as [S4] did), and an `AppEdition.Current` (App, `Hosting`) maps it to `AppEnvironment`. Nothing at run time can change it: no setting, argument or environment variable is read.
- **Core keeps the names.** `AppEnvironmentResolver.FromPackageName` is replaced by a Core type, `EditionProfile.For(AppEnvironment)`, that returns the edition's fixed names:

  | | PROD | DEV |
  |---|---|---|
  | Velopack packId | `MordWare` | `MordWare.Dev` |
  | Installer title | mordWare | mordWare (DEV) |
  | Data folder name | `mordWare Data` | `mordWare Data (DEV)` |

  It is pure and unit-tested. The packId and title are also consumed by `scripts/pack.ps1`, which keeps its own copy guarded by a check (D7). The localized window title and DEV marker stay in `Resources.resw`, as today.
- **Checked per configuration.** The App's csproj also emits `[assembly: AssemblyMetadata("MordWare.Edition", "PROD"|"DEV")]` from the same condition. A new architecture-test-project test reads that attribute from the App's build output by metadata only (the same loader the architecture tests use, never `Assembly.Load`). It asserts DEV in a Debug test run and PROD in a Release run, and CI runs Release. This covers the spec scenario "Automated check of the edition per configuration".

*Alternative considered:* reading the edition from Velopack's packId at run time. Rejected: a build-output DEV has no Velopack install, and a run-time source of truth is what guardrail 7 forbids.

### D3. Data paths: `LocalAppDataPaths` replaces `PackagedAppPaths`

- **The class.** `MordWare.App.Hosting.LocalAppDataPaths : IAppPaths` resolves `Environment.SpecialFolder.LocalApplicationData` plus `EditionProfile.DataFolderName`.
  - `DataFolder` is that root itself, so the database lives directly in `mordWare Data\` from Foundation onward.
  - `LogsFolder` is `<root>\Logs`.
  - Both are created on first use.
  - The data root is a sibling of Velopack's `%LOCALAPPDATA%\MordWare\` and never inside it ([S4] (b)).
- **Pattern.** It follows the existing `IAppPaths` pattern (`docs/architecture.md`, Editions): Core owns the contract and the App implements it.
- **Architecture rule 4** is updated to name `LocalAppDataPaths` and `AppHost`, with the same strength: test assemblies may not depend on them.
- **Unit-tested without touching real folders.** The folder-name logic lives in Core and is unit-tested. `LocalAppDataPaths` takes the local-app-data root in an internal constructor, so it can be exercised, but only from the App, never from tests (rule 4). Its correctness is verified in the running DEV app instead.
- **Data separation (guardrail 7).** PROD and DEV differ in the folder name, which comes from the build constant. Windows paths ignore case, and the names differ by more than case. Velopack's uninstall and a reinstall delete only `%LOCALAPPDATA%\<packId>\`, which contains neither data root.

### D4. Entry point and language

- **Entry point.** Define `DISABLE_XAML_GENERATED_MAIN` and add `Program.Main` (`[STAThread]`): `VelopackApp.Build().Run()` first, then `ComWrappersSupport.InitializeComWrappers()` and `Application.Start` with a `DispatcherQueueSynchronizationContext`, as in [S4].
  - No `OnFirstRun` customization: a normal install starts the app once, which is the desired behavior.
  - No logger is attached. Velopack's messages contain user-profile paths (guardrail 6), and logging is Foundation's.
- **Language.** `App.ApplyAppLanguage` switches to `Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride` (Windows App SDK). The .NET culture lines stay. Startup order stays: language first, then `AppHost.Create`, then the window.

### D5. Trimmed Windows App SDK

The `Microsoft.WindowsAppSDK` metapackage pulls in ML, AI, Search and Widgets, about 59 MB of the [S4] output and all of Windows ML. Directory.Packages.props replaces it with the component packages the app needs, at the same version (2.6.5).

- **Expected set:** `Microsoft.WindowsAppSDK.Foundation`, `.WinUI`, `.InteractiveExperiences`, `.DWrite` and `.Base`, plus `.Runtime` only if the self-contained build needs it.
- **Apply determines the minimal set** that builds, publishes and opens the installed window, and records it in `THIRD-PARTY-NOTICES` and the guardrail 1 text.
- A verification asserts that no `onnxruntime*`, `DirectML*` or `Microsoft.Windows.AI*` file is in the publish output.

*Alternative considered:* keeping the metapackage and deleting files after publish. Rejected: it is fragile, and it would still mean shipping those components' license terms.

### D6. Licensing: section 7 additional permission and notices

- **`LICENSE` stays the unmodified GPL-3.0 text** (`public-repository` spec).
- **A new root file `LICENSE-EXCEPTION.txt`** holds the additional permission. It is modeled on the FSF GPL FAQ's template for linking a GPL program with a non-free library (https://www.gnu.org/licenses/gpl-faq.html#GPLIncompatibleLibs). It names exactly:
  - the Microsoft Windows App SDK runtime components shipped (the D5 set);
  - the Windows SDK projection for .NET (`Microsoft.Windows.SDK.NET`).

  It grants permission to combine with and convey them. The corresponding source obligation then excludes their source, which Microsoft does not publish under these terms. The exact text is drafted in apply and reviewed by the owner before archive.
- **README license section:** "GPL-3.0-only, with an additional permission under section 7 for the Microsoft platform runtime (see `LICENSE-EXCEPTION.txt`)". It also states the guardrail 2 disclosure.
- **`THIRD-PARTY-NOTICES.txt`** (root) lists every redistributed component with its license:
  - MIT: the .NET runtime, Velopack, CommunityToolkit.Mvvm and the C#/WinRT runtime;
  - BSD-3-Clause: the WebView2 loader;
  - Microsoft Software License Terms: the Windows App SDK components and the Windows SDK projection, each with the full terms text, copied from the package's `license.txt`, and the Windows App SDK's own third-party notices.

  It is hand-maintained. A test in the architecture-test project checks that every `PackageReference` of the App project, and every package named in an allow-list of known transitive redistributables, has an entry. A new dependency therefore cannot ship unlisted.
- **What the installer ships.** `scripts/pack.ps1` copies `LICENSE`, `LICENSE-EXCEPTION.txt` and `THIRD-PARTY-NOTICES.txt` into the publish folder, and a pack check refuses a package without them.
- **Guardrail 1 wording (CLAUDE.md):** free, OSS-licensed, GPL-3.0-compatible dependencies only; the single named exception is the Microsoft platform runtime the app cannot run without (the D5 Windows App SDK components and the Windows SDK projection), redistributed under Microsoft's terms and covered by `LICENSE-EXCEPTION.txt`; any other non-OSS component is refused.
- **Guardrail 2 wording:** mordWare itself sends nothing about the user; the bundled Microsoft platform runtime may record usage data to Windows, which handles it according to the user's Windows diagnostic-data setting, as the README states.
- **`Directory.Packages.props` comments** name each package's real license (the `App (MIT)` comment is wrong today).
- **Microsoft's distribution condition 3b.ii** (end users agree to protective terms) is handled by shipping the terms in the notices with a README statement. This is a reading of Microsoft's license, recorded in the roadmap's v1.0.0 notes so it can be reviewed before the first release.

### D7. Packing: `scripts/pack.ps1`

`scripts/pack.ps1 -Edition Prod|Dev -Version <semver> [-OutputDir <path>]` (PowerShell 7) packs one edition:

1. `dotnet publish` the App: `-c Release` for Prod, `-c Debug` for Dev, `-r win-x64`, with the deterministic properties from D8.
2. Copies the three legal texts into the publish folder and refuses to continue if one is missing.
3. Runs `dotnet vpk pack` with the edition's packId and title, the default channel (`win`), `--shortcuts StartMenuRoot`, no portable zip (if vpk 1.2.161 has no switch to skip the portable zip, it is deleted from the output and never uploaded), and the edition's icon.
4. Runs `check-public-safety.ps1 -Package <output>` (D9).

Further rules:
- **Name guard.** The script has its own packId/title table, and refuses any edition/name pair outside it. A Pester-free self-check (`-SelfTest`, the pattern of the safety script) proves that Dev never yields `MordWare` and Prod never `MordWare.Dev`.
- **Pinned `vpk`.** `vpk` comes from a root `.config/dotnet-tools.json` pinned to 1.2.161 (MIT). `Velopack` 1.2.161 (MIT) goes into `Directory.Packages.props`.
- **Versions.**
  - Releases use the tag's version.
  - The dry run uses `0.0.0-dryrun.<run number>`.
  - Local check builds use `0.0.<n>`, so two of them can test an upgrade.
  - The App's `Version` defaults to `0.0.0-dev` and is overridden by `-p:Version=` from `pack.ps1`.

*Alternative considered:* packing only inside the workflow. Rejected: the check-only DEV installer must be buildable locally (decision A1), and one script keeps local and CI packing identical.

### D8. Deterministic, path-free binaries

These properties are set in `Directory.Build.props` for all `src/` projects, as in [S4] (i):
- `Deterministic=true`;
- `PathMap=$(MSBuildProjectDirectory)=/_/` (or a repo-root map);
- `DebugType=none` in Release.

`ContinuousIntegrationBuild=true` is set when `pack.ps1` or CI builds. Debug keeps PDBs for local debugging; the check-only DEV package ships whatever Debug produces, but it is never published, and the scan still runs on it.

### D9. Package scan in `check-public-safety.ps1`

A new `-Package <folder>` mode folds in `spikes/S4.Installer/scan-package.ps1`:
- It extracts the `.nupkg` produced by vpk and scans `Setup.exe`, the nupkg contents and the publish folder.
- It looks at ASCII and UTF-16 strings, using the existing user-path, package-hash, e-mail, private-key and private-word rules.
- **Own files** (`MordWare.*` assemblies, `.pri`, `.xbf`, resources) must be clean.
- **A third-party file** passes only if its SHA-256 equals a file in its upstream NuGet package. The script looks in the NuGet global packages folder, which restore populates in CI.
- It reports the accepted third-party files.
- `-SelfTest` gains a sample: a planted own file with a user path must fail, and a clean one must pass.

### D10. Release workflow: `.github/workflows/release.yml`

- **Triggers:** `push: tags: ['v*.*.*']` and `workflow_dispatch` (the dry run). Superseded runs are not cancelled for tags.
- **Job `release`** (`windows-latest`, `timeout-minutes: 30`, `permissions: contents: read`):
  1. Full checkout.
  2. For tags, check the tag matches `^v\d+\.\d+\.\d+$` and that `git merge-base --is-ancestor <sha> origin/main` succeeds.
  3. Check that `CHANGELOG.md` has a `## [X.Y.Z]` section.
  4. Restore, build and test (Release).
  5. `pack.ps1 -Edition Prod -Version <version>`, which includes the scan.
  6. Upload the Velopack output as a workflow artifact (always, retention 7 days).
- **Job `publish`**, tags only, `needs: release`, `permissions: contents: write`:
  1. Download the artifact.
  2. Write the notes file: the changelog section, then "mordWare is licensed GPL-3.0-only with an additional permission (LICENSE-EXCEPTION.txt). Source code for this version: https://github.com/KingMordas/mordWare/tree/vX.Y.Z" plus GitHub's automatic source archives.
  3. Run `dotnet vpk upload github --repoUrl ... --publish --tag vX.Y.Z --releaseName "mordWare X.Y.Z" --targetCommitish <full sha>` with `VPK_TOKEN: ${{ secrets.GITHUB_TOKEN }}` in the environment, never on the command line.
  4. Set the notes with `gh release edit --notes-file` if vpk does not take them.
- **The checks live in a script, not inline.** The tag-format, on-`main` and changelog checks of steps 2–3 and the notes file of `publish` step 2 sit in `scripts/release-prep.ps1` (`-Version`, `-Sha`, `-NotesFile`, plus `-SelfTest`), so they can be tested locally against a throwaway repository, the pattern of `check-public-safety.ps1 -SelfTest`. The `release` job writes the notes file in both modes and adds it to the artifact, so the dry run shows the exact notes a release would get.
- **The `publish` job never runs for `workflow_dispatch`**, so the dry run cannot publish (spec "Release dry run").
- **CI (`ci.yml`)** keeps its role. Its build drops `-p:GenerateAppxPackageOnBuild=false` and the Release-restore workaround once nothing packages and ReadyToRun is off. It does not pack.

*Alternative considered:* `softprops/action-gh-release` for publishing. Rejected: a third-party action with write access, when vpk already uploads and creates the release.

### D11. `run-dev.ps1`

- **Rewritten** (Windows PowerShell 5.1 and 7): `dotnet build` of the App in Debug for x64, then `Start-Process` the built `MordWare.App.exe` from `bin\x64\Debug\...\win-x64\`.
  - `-NoBuild` starts the existing build.
  - `-Reset` deletes the `mordWare Data (DEV)` folder after confirmation. It resolves that folder from the same name table as D2, and has a name guard that refuses anything not ending in `(DEV)`.
  - The Developer Mode check is removed.
- **Leftover MSIX registration.** If a `MordWare.Dev` registration from the MSIX era exists, the script offers once to remove it (`Remove-AppxPackage`, exact name match, DEV only). That removes an app registration, not a Windows setting.
- **Never touches PROD:** it never builds Release, never touches `%LOCALAPPDATA%\MordWare\` and never touches `mordWare Data`.

### D12. Architecture-test loader

`FindBuildOutput` keeps its search over `bin\<configuration>\` and `bin\<platform>\<configuration>\`. Its comment changes to x64, and the `AppX` exclusion is removed with the packaging; with no AppX layout, it would only hide a mistake. Rules stay as strict: rule 4 names the new class, and "a rule that selects nothing fails" still holds.

### D13. Spike and docs clean-up

- **Delete `spikes/S4.Installer`.** The unpackaged project is adopted (D1), `Program.Main` too (D4), and so are `run-checks.ps1` as `pack.ps1` with the check procedure in tasks (D7), and `scan-package.ps1` as `-Package` (D9).
- **`docs/architecture.md`:**
  - Editions, data paths, running, delivery and CI are rewritten for the unpackaged app.
  - The "Packaging is changing" banner is removed.
  - The S4 spike entry is removed from the spikes section.
- **`docs/public-safety.md`** gains the package-scan step.
- **`README.md`:**
  - no Developer Mode;
  - the new `run-dev.ps1` usage;
  - the license and privacy statements (D6);
  - the first-install wording from the roadmap draft. Its Smart App Control sentence is marked as provisional until the v1.0.0 decision.
- **`CLAUDE.md`:** guardrails 1, 2 and 7, the Delivery and Stack lines, and the edition mechanism.
- **`docs/roadmap.md`:**
  - [D] → `in progress`, then `done` at archive;
  - the [S4] table row is fixed to `2026-11-05-spike-unpackaged-installer` (archived);
  - the Smart App Control decision and the 3b.ii reading move to the v1.0.0 item.

## Risks / Trade-offs

- **[Microsoft license reading]** The section 7 permission and the notice-based handling of Microsoft's distribution conditions are a good-faith reading, not legal advice. → Both texts are reviewed by the owner before archive, the reading is recorded in the roadmap's v1.0.0 notes, and a commercial or Store edition would get proper advice first.
- **[Telemetry is inferred, not proven]** Microsoft says the runtime collects usage data; the Windows-pipeline path is an inference. → The README states only what is known, and the app's own no-connection check is a task. A deeper trace is optional, and only one that changes no Windows setting.
- **[Trimmed SDK misses a needed component]** Removing the metapackage could drop something WinUI needs at run time. → The installed-window check (not only the build output) runs on the check-only DEV install. If a component turns out to be required, it is added to D5, to the notices and to the guardrail 1 list in the same change.
- **[Dry run is the only release test]** The publish job cannot be exercised before v1.0.0. → The dry run covers every step but upload. The publish job is short and uses the same vpk upload that [S4] (h) proved. Its token is limited to that job.
- **[Pushing is owner-gated]** The dry run needs the workflow on GitHub, which means a push. → The push and the dry-run trigger are tasks that wait for the owner's explicit approval.
- **[Setup over an existing install too soon]** Velopack's Setup fails if started within about 2 s of the previous run ([S4]). → `pack.ps1` documents it, and the check procedure waits before an upgrade check.
- **[Old MSIX DEV data]** A leftover `MordWare.Dev` registration's LocalState holds only test output from the bootstrap era. → `run-dev.ps1` removes it only after confirmation. There is no real data in it (no persistence existed).

## Migration Plan

1. Land the change on `main`. DEV moves to the build output on the next `run-dev.ps1`. No user data exists to migrate.
2. With the owner's approval: push, then start the dry run once, then download the artifact and inspect it (scan report, legal files, no DEV package).
3. Rollback: revert the commit. No release, tag or data depends on it.

## Open Questions

- The exact minimal Windows App SDK component set (D5), and whether vpk 1.2.161 can skip the portable zip (D7). Both are answered while applying and change neither the specs nor the task breakdown.
