# Tasks

## 1. Build setup and dependencies

- [x] 1.1 In `Directory.Packages.props`, replace the `Microsoft.WindowsAppSDK` metapackage with the component packages from design D5 (2.6.5), add `Velopack` 1.2.161, remove `Microsoft.Windows.SDK.BuildTools.WinApp`, and give each entry a comment with its real license (no more `App (MIT)`); verify `dotnet restore MordWare.slnx` succeeds.
- [x] 1.2 Add a root `.config/dotnet-tools.json` pinning `vpk` 1.2.161; verify `dotnet tool restore` then `dotnet vpk --help` run.
- [x] 1.3 Add the deterministic, path-free build properties to `Directory.Build.props` (design D8); verify a Release build of the solution succeeds with warnings as errors.
- [x] 1.4 Map the App to x64 in `MordWare.slnx` and drop its x86/ARM64 platforms; verify `dotnet build MordWare.slnx -c Debug` produces `src/MordWare.App/bin/x64/Debug/...`.

## 2. Editions and data paths

- [x] 2.1 In `MordWare.Core/Editions`, replace `AppEnvironmentResolver` with `EditionProfile.For(AppEnvironment)` (packId, installer title, data folder name; design D2), and update the `AppEnvironment` doc comments; verify with focused unit tests (`--filter-class "*EditionProfile*"`) that PROD gives `MordWare` / "mordWare" / `mordWare Data`, DEV gives `MordWare.Dev` / "mordWare (DEV)" / `mordWare Data (DEV)`, and the folder names differ case-insensitively and neither contains the other (scenario "Editions resolve different data folders").
- [x] 2.2 Remove the old `FromPackageName` tests and keep every other Core test green; verify with `dotnet test --project tests/MordWare.Tests.Unit --filter-class "*Edition*"`.
- [x] 2.3 In the App, define `MORDWARE_PROD` and the `MordWare.Edition` assembly metadata only for Release, and add `Hosting/AppEdition` (design D2) reading nothing at run time; verify by review that it reads no argument, setting or environment value (scenario "No run-time switch").
- [x] 2.4 Replace `PackagedAppPaths` with `Hosting/LocalAppDataPaths` (DataFolder = data root, LogsFolder = `<root>\Logs`; design D3), and switch `AppHost.Create` to `AppEdition` + `LocalAppDataPaths`; verify the solution builds.
- [x] 2.5 Update architecture rule 4 to `LocalAppDataPaths` and `AppHost`, and update `FindBuildOutput` (x64 comment, no AppX exclusion; design D12); verify `dotnet test --project tests/MordWare.Tests.Architecture` passes in Debug and Release (scenario "Automated tests never touch real data").
- [x] 2.6 Add the architecture-project test that reads the App's `MordWare.Edition` metadata by path and asserts DEV for a Debug run and PROD for a Release run; verify it passes in both configurations and fails when the expected value is flipped locally (scenarios "Automated check of the edition per configuration" and "Release build produces PROD", first half).
- [x] 2.7 Rewrite the Editions and data-path sections of `docs/architecture.md`, the architecture-tests section (rule 4, loader) and guardrail 7's mechanism sentence in `CLAUDE.md`; verify both describe the build constant, both data roots and the check-only DEV installer, with no MSIX mechanism left.

## 3. Unpackaged app, entry point, language and DEV workflow

- [x] 3.1 Make `MordWare.App.csproj` unpackaged per design D1 (keep `EnableMsixTooling=true`, ReadyToRun off), delete both `Package*.appxmanifest` files and the publish profiles, and set each edition's exe icon (`ApplicationIcon`) from `Assets/` or `Assets/Dev/`; verify Debug and Release builds succeed and the build output contains `MordWare.App.exe` and its `.pri`.
- [x] 3.2 Add `Program.Main` with `VelopackApp.Build().Run()` first and `DISABLE_XAML_GENERATED_MAIN` (design D4); verify the DEV build output starts and opens its window.
- [x] 3.3 Switch `App.ApplyAppLanguage` to `Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride`; verify the DEV window shows its en-US title, and the it-IT title and DEV marker after temporarily switching the default language in a local, uncommitted edit (scenario "DEV window shows the marker"; screenshots en-US and it-IT; close every window afterwards).
- [x] 3.4 Verify the trimmed SDK: the publish output contains no `onnxruntime*`, `DirectML*` or `Microsoft.Windows.AI*` file, and the installed-style window still opens; note the final component set for tasks 4.1, 4.2 and 8.3.
- [x] 3.5 Rewrite `scripts/run-dev.ps1` per design D11 (build Debug x64, start the exe, `-NoBuild`, `-Reset` with a `(DEV)` name guard, a one-time offer to remove a leftover `MordWare.Dev` MSIX registration, no Developer Mode check); verify in Windows PowerShell 5.1 and PowerShell 7 that it launches "mordWare (DEV)" (scenario "Debug run starts DEV"), and that `-Reset` deletes only `mordWare Data (DEV)`.
- [x] 3.6 Verify that the DEV edition running from the build output creates and uses only `%LOCALAPPDATA%\mordWare Data (DEV)\`, and that `mordWare Data` and `%LOCALAPPDATA%\MordWare\` are not created (scenario "DEV from the build output uses the DEV folder").
- [x] 3.7 With the owner's explicit approval, and only while `%LOCALAPPDATA%\mordWare Data\` does not exist: start the Release build output once next to the running DEV build, and check that both windows run at the same time with their own names, that PROD shows no DEV marker and the PROD icon, and that closing one leaves the other running (scenarios "Both editions run side by side" and "PROD window shows no marker"). Then close both, and delete the `mordWare Data` folder this check created; if it already existed, skip the check and record why.
- [x] 3.8 Update the CI build in `.github/workflows/ci.yml` (no `GenerateAppxPackageOnBuild`, no ReadyToRun restore workaround), and the running/debugging and CI sections of `docs/architecture.md`; verify a local `dotnet build MordWare.slnx -c Release` then `dotnet test --solution MordWare.slnx -c Release --no-build` pass, as CI runs them.
- [x] 3.9 Update `README.md` (requirements without Developer Mode, the new `run-dev.ps1` usage, editions paragraph) and the `CHANGELOG.md` `## [Unreleased]` entries (installable per user through an installer; DEV runs from the build output and is no longer installed as a package); verify every command in the README runs as written.

## 4. Licensing, notices and guardrails

- [x] 4.1 Draft `LICENSE-EXCEPTION.txt` (GPL-3.0 section 7 additional permission, modeled on the FSF GPL FAQ template, naming exactly the Windows App SDK components of task 3.4 and the Windows SDK projection; design D6), leaving `LICENSE` unchanged; verify `git diff --stat LICENSE` is empty, and that the owner has read and approved the text (scenario "The additional permission is narrow").
- [x] 4.2 Write `THIRD-PARTY-NOTICES.txt` with every redistributed component and its license, including the full Microsoft terms copied from the packages; verify against the publish output that every third-party file's package is listed.
- [x] 4.3 Add the architecture-project test that every App `PackageReference` and every allow-listed transitive redistributable has a `THIRD-PARTY-NOTICES.txt` entry; verify it passes, and fails when an entry is removed locally.
- [x] 4.4 Reword guardrails 1 and 2 in `CLAUDE.md` (design D6), and update the README license section (GPL-3.0-only plus the additional permission) and its privacy statement (mordWare sends nothing; the Microsoft platform runtime may record usage data that Windows handles according to the user's diagnostic-data setting); verify scenarios "Visitor reads the terms", "No later-version grant" and "Platform runtime data is disclosed" by reading the rendered README.

## 5. Packing and package scan

- [x] 5.1 Add the `-Package <folder>` mode to `scripts/check-public-safety.ps1` (design D9) with its `-SelfTest` samples; verify `-SelfTest` passes, including the planted own-file user path failing (scenario "Local path in the app's own files").
- [x] 5.2 Add `scripts/pack.ps1` (design D7: publish, copy the legal texts, refuse when one is missing, vpk pack with the edition's names and `StartMenuRoot`, no portable zip, then `-Package` scan) with a `-SelfTest` name guard; verify `-SelfTest` passes, and that a local `-Edition Prod -Version 0.0.1` pack produces `Setup.exe` and a full package that pass the scan (scenario "Clean package passes"). Do not run that Setup.exe.
- [x] 5.3 Add the packing and package-scan step to `docs/public-safety.md`, and a packing section (editions, versions, check-only DEV installer, the 2 s Setup caveat) to `docs/architecture.md`; verify the documented commands run as written.

## 6. Release workflow

- [x] 6.1 Add `scripts/release-prep.ps1` (design D10: tag format, tagged commit on `main`, changelog section, notes file with the license line and the version's source link; in dry-run mode the notes use the Unreleased section) with `-SelfTest` on a throwaway repository; verify `-SelfTest` passes, covering a tag off `main` refused (scenario "Tag off main is refused") and a missing changelog section refused (scenario "Missing changelog section").
- [x] 6.2 Add `.github/workflows/release.yml` (design D10: `release` job with read-only permissions, timeout, prep, build, test, pack, artifact with notes; `publish` job only for tags, `contents: write`, `VPK_TOKEN` from `GITHUB_TOKEN`); verify by review that packing runs only after tests in the same job (scenario "Failing tests block the release"), that `publish` cannot run for `workflow_dispatch`, and that no DEV edition is ever packed (scenario "Release contains only PROD", first half).
- [x] 6.3 Document the release process (tag rules, dry run, versions, notes, what publishing needs) in `docs/architecture.md` and the release line in `CLAUDE.md`; verify both match `release.yml`.

## 7. Installer checks (check-only DEV installer)

- [x] 7.1 Before the checks, record whether `%LOCALAPPDATA%\MordWare\` and `%LOCALAPPDATA%\mordWare Data\` exist and their last-write times; after every check in this group, verify both are unchanged (scenarios "Installer checks never use PROD" and "DEV data untouched by PROD installs", mirrored on the DEV side; the PROD side is covered by tasks 2.1 and 5.2's name guard and by [S4] (c)).
- [x] 7.2 Pack DEV `0.0.1` and `0.0.2` with `pack.ps1 -Edition Dev`, install `0.0.1` without elevation, and verify: no elevation prompt, a Start menu entry "mordWare (DEV)" with the DEV icon, an installed-apps entry, no desktop shortcut, and the installed window opens with its en-US title and DEV marker (scenarios "Install from the installer", "Installed app opens its window" and "Check-only DEV install is labeled"; screenshot, then close the window).
- [x] 7.3 Verify the installation folder contains `LICENSE`, `LICENSE-EXCEPTION.txt` and `THIRD-PARTY-NOTICES.txt` (scenario "Installed files carry the legal texts").
- [x] 7.4 With the installed `0.0.1` running and its window open, verify for at least three minutes that no TCP connection is owned by its process (for example by sampling `Get-NetTCPConnection -OwningProcess`, which changes no setting), then close it (scenario "Idle installed app").
- [x] 7.5 Write a marker file into `mordWare Data (DEV)`, wait a few seconds after the app exits, install `0.0.2` over `0.0.1`, and verify one Start menu entry, version 0.0.2 running, and the marker file unchanged (scenarios "Newer installer over an older install" and "Upgrade keeps the data").
- [x] 7.6 Uninstall the DEV check install and verify that the program folder, the Start menu entry and the installed-apps entry are gone while `mordWare Data (DEV)` and the marker remain (scenarios "Uninstall keeps the data" and "Uninstalling DEV leaves PROD intact" and "Both editions installed side by side", DEV side; the PROD side is [S4] (c) evidence plus the distinct packIds of task 2.1); then delete the marker and the local pack output, and record the results in generic terms.

## 8. Clean-up, roadmap and final checks

- [x] 8.1 Delete `spikes/S4.Installer` and its entries in `spikes/Directory.Packages.props`, and remove the S4 entry from the spikes section of `docs/architecture.md`; verify `git grep -n "S4.Installer"` finds only the archived change and the roadmap.
- [x] 8.2 Update the README's "Getting mordWare" section with the first-install wording (unsigned warnings, download only from Releases, no admin rights, where the data stays and how to remove it; the Smart App Control sentence marked provisional); verify scenario "Visitor reads how to install" by reading the rendered README.
- [x] 8.3 Update `docs/roadmap.md`: [D] to `in progress` with change `delivery-unpackaged`; fix the [S4] table row to `2026-11-05-spike-unpackaged-installer` (archived); move the Smart App Control decision and the Microsoft 3b.ii reading into the v1.0.0 item's parked notes; record the final Windows App SDK component set; verify the "what's next" procedure still reads correctly.
- [ ] 8.4 With the owner's explicit approval to push: push `main`, start the release dry run from GitHub, and verify the run passes, its artifact holds only the PROD installer and full package (packId `MordWare`, title "mordWare", version `0.0.0-dryrun.<n>`) plus the notes file with the license line and source link, the scan report lists only upstream-identical third-party files, and no release or tag was created (scenarios "Dry run publishes nothing", "Release contains only PROD", "Release build produces PROD" second half, and "Release page offers the source" by its notes). The live publish path (scenario "Tag on main publishes") stays unexercised until v1.0.0, as recorded in the roadmap.
- [x] 8.5 Run `scripts/check-public-safety.ps1 -All` with the local deny list, and `-SelfTest`; verify both pass before archive.

## Workflow follow-up

- Run `/opsx:archive` once every task is done: it marks [D] done in `docs/roadmap.md`, syncs the four specs and ends with one Conventional Commit on `main`.
- Push only when the owner asks.
