# Tasks

Hard constraint for every task: **no Windows setting, policy or optional feature on this PC is changed or even attempted.** That includes Smart App Control, SmartScreen, Defender, Developer Mode, Windows Sandbox and execution policy. A step that would need one is recorded as NOT VERIFIED, with the documentation used instead. Results are recorded in generic terms only: no user-folder paths, machine names, package-family hashes or personal values.

## 1. Spike scaffolding

- [x] 1.1 In `docs/roadmap.md`, mark [S4] `in progress` with change `spike-unpackaged-installer`. Verify that the table and the [S4] section both show it.
- [x] 1.2 Pin Velopack (latest stable) in `spikes/Directory.Packages.props`, and `vpk` in the local tool manifest `spikes/S4.Installer/.config/dotnet-tools.json` (design D5). Record both versions and confirm both licenses are MIT.
  - Verify:
    - `dotnet tool restore` inside the spike folder installs `vpk` locally, and `dotnet tool list -g` is unchanged
    - `git diff --stat` shows the root `Directory.Packages.props`, `MordWare.slnx` and `src/` unchanged
  - Result (2026-11-05): PASS. Velopack 1.2.161 and vpk 1.2.161, both MIT (nuspec license expression); Velopack's only dependencies are Newtonsoft.Json (MIT) and Microsoft.Win32.Registry (MIT) on older frameworks. `dotnet tool restore` restored vpk locally; the global tool list is unchanged. The root `Directory.Packages.props`, `MordWare.slnx` and `src/` are unchanged.
- [x] 1.3 Create the probe `spikes/S4.Installer/S4.Installer.csproj` (design D1, D4): unpackaged WinUI 3, self-contained .NET and Windows App SDK, win-x64 only, `MORDWARE_PROD` only in Release, deterministic build with `PathMap` and no PDBs in published output. It is not added to `MordWare.slnx`.
  - Verify:
    - Debug and Release both build
    - `dotnet build MordWare.slnx` is unaffected
  - Result (2026-11-05): PASS. Debug and Release build with 0 warnings; `dotnet build MordWare.slnx` still builds with 0 warnings and 0 errors. The assembly is `S4Probe`; the build output carries `S4Probe.pri` next to the exe (unpackaged MRT Core resources).
  - Velopack 1.2.161 API notes for [D]/[U]: `UpdateManager.Channel` and `IsNonDefaultChannel` are not public; `HttpClientFileDownloader.UserAgent` is static; `GithubSource(repoUrl, accessToken, prerelease, IFileDownloader)` accepts a custom downloader, which the probe uses to log requests.
- [x] 1.4 Add the custom entry point (design D2): `DISABLE_XAML_GENERATED_MAIN`, then `Program.Main` running `VelopackApp.Build().Run()`, then command dispatch, then WinUI start. Add the `where` and `version` commands, writing `results\<command>.json` under the edition's spike data root.
  - Verify, from the Debug build output:
    - `where` reports edition DEV, the DEV spike data root and `IsInstalled: false`
    - the window opens with no command (closed right after)
  - Result (2026-11-05): PASS. From the Debug build output, `S4Probe.exe where` exits 0 in its own process: edition DEV, packId `MordWare.Spike.S4.Dev`, the DEV spike data root, `isInstalled: false`, not elevated, X64. `entry.json` shows the Velopack hook ran before the command. With no command the window opens titled "mordWare S4 spike (DEV)", responding; it was closed right after.
- [x] 1.5 Add the driver `spikes/S4.Installer/run-checks.ps1` (design D5): `Pack`, `Install`, `Uninstall`, `-CleanupOnly`, and the name guard.
  - Verify:
    - a helper called with `MordWare`, `MordWare.Dev` or a `mordWare Data` path throws before acting
    - `-CleanupOnly` on a clean PC is a no-op that exits 0
  - Result (2026-11-05): PASS. `-SelfTestGuard`: the guard refuses `MordWare`, `MordWare.Dev`, `%LOCALAPPDATA%\mordWare Data` and `%LOCALAPPDATA%\mordWare Data (DEV)` (both the delete and the install-folder helpers throw). `-CleanupOnly` exits 0 and, run a second time on the cleaned PC, is a no-op that exits 0.
  - Finding: `Setup.exe` is a GUI-subsystem executable. Started with `--help` through the PowerShell call operator, it printed nothing and kept running invisibly (it installed nothing) until it was stopped; with `Start-Process -RedirectStandardOutput -Wait` it prints its help and exits 0. Options: `--silent`, `--verbose`, `--log <file>`, `--installto <dir>`, and `-- <args>` for the started app.
  - `vpk pack` defaults to Desktop and Start menu shortcuts; the spike passes `--shortcuts StartMenuRoot`.
- [x] 1.6 Update the `spikes/` paragraph of `docs/architecture.md` to name `S4.Installer`: an unpackaged probe with Velopack, run on this PC only, not in CI, using spike packIds and spike data roots. Verify that the paragraph matches the files created above.
  - Result (2026-11-05): done. The `spikes/` paragraph names `S4.Installer` (an exception to the xUnit shape, like S3) with its packIds, driver, guard, tool manifest and folders, matching the files created above.

## 2. Unpackaged runtime (checks d, j)

- [x] 2.1 Run check (j): from both the build output and an installed copy, Velopack's hook runs before any UI, and the WinUI window then opens normally. Verify with the driver's step: process exit codes, `where.json` written, window title read through the process's `MainWindowTitle`. Close the window afterwards.
  - Result (2026-11-05): PASS, after one fix.
    - From the Release build output and from an installed copy (`%LOCALAPPDATA%\MordWare.Spike.S4\current\`), the probe writes `entry.json` (Velopack's `Run` completed before any UI) and then opens its window titled "mordWare S4 spike"; both windows were closed by the driver right after. The Velopack log shows the install hook (`--veloapp-install`) handled as a fast exit, and the locator finding `Update.exe` in the parent folder only for the installed copy.
    - The installed copy also loads the it-IT `.resw` string ("Ciao dallo spike") from its `S4Probe.pri`.
    - **Finding and fix: `EnableMsixTooling` must stay `true` in an unpackaged app.** With it set to `false`, `dotnet publish` drops the app's own `S4Probe.pri` (which carries the compiled XAML: `App.xbf`, `MainWindow.xbf`). The build output still has them, so runs from it work, but the installed app crashed on its first window in `Microsoft.UI.Xaml.dll` (0xc000027b, Application log). Headless commands, which load no XAML, still worked. With `EnableMsixTooling=true` and `WindowsPackageType=None` the publish output carries `S4Probe.pri` and the installed window opens. For [D]: keep `EnableMsixTooling` true while removing the manifests, and test the installed window, not only the build output.
- [x] 2.2 Add the `lang <tag>` command and two `.resw` strings in `Strings/en-US` and `Strings/it-IT` (design D4). Run check (d):
  - the Windows App SDK `PrimaryLanguageOverride` works unpackaged
  - the `.resw` strings load in both languages
  - the `Windows.Globalization` version throws unpackaged (evidence for [D])
  - the edition follows the build constant (Debug = DEV, Release = PROD-like)
  - a run from the build output skips update checks without an error
  - Verify with the driver's PASS / FAIL step, which reads `lang-en-US.json`, `lang-it-IT.json` and `where.json`.
  - Result (2026-11-05): PASS, for both the Debug (DEV) and Release (PROD-like) build outputs, each in a fresh process:
    - `Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride` (Windows App SDK) works unpackaged, and MRT Core's `ResourceLoader` follows it: en-US gives "Hello from the spike" / "DEV", it-IT gives "Ciao dallo spike" / "SVILUPPO". Dates and numbers follow `CultureInfo` ("Thursday, November 5, 2026" / "1,234.50" and "giovedì 5 novembre 2026" / "1.234,50").
    - The `Windows.Globalization` override the app uses today throws `InvalidOperationException` unpackaged: [D] must switch `App.ApplyAppLanguage` to the Windows App SDK version.
    - The edition follows the build constant: Debug = DEV / `MordWare.Spike.S4.Dev`, Release = PROD / `MordWare.Spike.S4`.
    - Run from the build output, `isInstalled` is false and `check-update` skips with "update checks are off", no error.

## 3. Install, upgrade, uninstall and data survival (checks a, b, c)

- [x] 3.1 Pack 0.1.0 (Release, PROD-like) into the local spike feed and run check (a): silent `Setup.exe` install with no elevation prompt and no admin rights, the Start menu entry present, then an upgrade to 0.1.1 over it, then Velopack uninstall. Verify with the driver step:
  - the install folder and the per-user uninstall entry appear, and are gone after uninstall
  - the process ran unelevated
  - `version` reports each version
  - Result (2026-11-05): PASS.
    - `Setup.exe --silent` (0.1.0) installs per user into `%LOCALAPPDATA%\MordWare.Spike.S4\` (`Update.exe`, `current\`), writes the per-user uninstall entry under HKCU, and creates only the Start menu shortcut "mordWare S4 spike". No elevation prompt; the probe reports `elevated: false` and appId `MordWare.Spike.S4`, version 0.1.0.
    - Setup runs the app once with `--veloapp-install <version>` (the install hook, handled by `VelopackApp.Run`). In silent mode it does NOT start the app afterwards, so no first-run hook fired (`firstrun.json` absent).
    - Running the 0.1.1 `Setup.exe` over the installed 0.1.0 upgrades it in place: the probe reports 0.1.1.
    - `Update.exe --uninstall --silent` exits 0 and removes the install folder, the uninstall entry and the Start menu shortcut.
- [x] 3.2 Run check (b) (design D3):
  - Write a marker into the spike data root and a trap marker into the lowercase packId folder name.
  - Upgrade, then uninstall.
  - Verify with the driver step: the data-root marker survives both, and the trap marker is gone after uninstall.
  - Result (2026-11-05): PASS.
    - The marker in the spike data root (`%LOCALAPPDATA%\mordWare S4 spike Data\`) survived the Setup 0.1.1 upgrade over 0.1.0 (the probe reads it back) and the uninstall.
    - The trap: `%LOCALAPPDATA%\mordware.spike.s4\trap.txt` (lowercase) is the same folder as the install folder `MordWare.Spike.S4`, and it was gone after uninstall.
    - **Finding: Setup over an existing install also wipes the packId folder.** Setup renames the whole `%LOCALAPPDATA%\<packId>\` to a rollback folder, installs fresh, then deletes the rollback folder. So even an upgrade by `Setup.exe` deletes anything else kept in the packId folder, not only an uninstall. This confirms the data root must never be inside it.
    - **Finding: a Setup over an existing install started within about 2 s of the previous install or app run fails.** The rename is denied (OS error 5); Velopack retries 4 times over 4 s, then exits 1 with "Failed to remove existing application directory, please close the application...". The same Setup succeeds after a 10 s or 30 s wait, every time. Reproduced with nothing run in between, so it is not caused by our files or our process (Defender real-time protection is off on this PC; the cause was not isolated further). The driver retries once after 15 s and says so. For [D]: a user who re-runs the installer right after installing may see this message; the in-app update path (check g) does not use this rename.
- [x] 3.3 Run check (c): install the DEV-like package (Debug, `MordWare.Spike.S4.Dev`) next to the PROD-like one.
  - Verify with the driver step:
    - two install folders, two data roots, two Start menu entries
    - each `where.json` reports its own edition and root
    - uninstalling DEV leaves the PROD-like install, its marker and its data intact
  - Then uninstall the PROD-like package too.
  - Result (2026-11-05): PASS.
    - The marker in the spike data root (`%LOCALAPPDATA%\mordWare S4 spike Data\`) survived the Setup 0.1.1 upgrade over 0.1.0 (the probe reads it back) and the uninstall.
    - The trap: `%LOCALAPPDATA%\mordware.spike.s4\trap.txt` (lowercase) is the same folder as the install folder `MordWare.Spike.S4`, and it was gone after uninstall.
    - **Finding: Setup over an existing install also wipes the packId folder.** Setup renames the whole `%LOCALAPPDATA%\<packId>\` to a rollback folder, installs fresh, then deletes the rollback folder. So even an upgrade by `Setup.exe` deletes anything else kept in the packId folder, not only an uninstall. This confirms the data root must never be inside it.
    - **Finding: a Setup over an existing install started within about 2 s of the previous install or app run fails.** The rename is denied (OS error 5), Velopack retries 4 times over 4 s, then exits 1 with "Failed to remove existing application directory, please close the application...". The same Setup succeeds after a 10 s or 30 s wait, every time. Reproduced with nothing run in between, so it is not caused by our files or our process (Defender real-time protection is off on this PC; the cause was not isolated further). The driver retries once after 15 s and says so. For [D]: a user who re-runs the installer right after installing may see this message; the in-app update path (check g) does not use this rename.

## 4. Folder picker (check e)

- [x] 4.1 Add the "Pick folder" and "Write test file" buttons, storing the path in `picker.json` in the spike data root (design D3). Run check (e) with the owner on an installed copy:
  - pick a folder outside Documents and AppData, write
  - close and relaunch, write again
  - Verify that the driver sees both files and that the path persisted. Close the probe window afterwards, and delete the picked test folder in cleanup.
  - Result (2026-11-05, installed copy, done by the owner): PASS.
    - The Windows App SDK `Microsoft.Windows.Storage.Pickers.FolderPicker(AppWindow.Id)` works in the unpackaged app. The owner picked a new folder under the user profile, outside Documents and AppData, and the first write landed there.
    - After closing and relaunching, the window showed the remembered folder (from `picker.json` in the spike data root, since `LocalSettings` does not exist unpackaged), and the second write landed there too: two `s4-picker-*.txt` files, nothing else in the folder.
    - Both test files were deleted and the spike was uninstalled; the now empty folder the owner created is left to the owner.

## 5. Package size and binary public safety (check i)

- [x] 5.1 Add `spikes/S4.Installer/scan-package.ps1` (design D7). Verify it with a self-test: a temp file containing a sample user-folder path in ASCII and in UTF-16LE is reported, and a clean file passes.
  - Result (2026-11-05): PASS. `scan-package.ps1 -SelfTest`: a sample user-folder path is reported both as ASCII and as UTF-16LE (at an odd byte offset), and a clean file passes.
- [x] 5.2 Run check (i) on the 0.1.0 and 0.1.1 full packages, the delta package and `Setup.exe`, with the private deny list present. Verify that no finding comes from the spike's own files; list third-party matches separately as evidence. Any finding in our files blocks group 7 until it is fixed and re-scanned.
  - Result (2026-11-05): PASS. Scanned the 0.1.0 and 0.1.1 full packages, the 0.1.1 delta, the feed files and both `Setup.exe` (2,098 files after expansion), with the private deny list present.
    - **No finding in our own files** (`S4Probe.*` and the package metadata): `Deterministic`, `ContinuousIntegrationBuild`, `PathMap` and `DebugType=none` leave no build path in the probe's binaries.
    - Each `Setup.exe` is a stub with its full `.nupkg` appended byte for byte (found at offset 7,514,112), so scanning the `.nupkg` covers the installer.
    - Third-party evidence, not blocking: user-folder paths of other machines inside Microsoft binaries and inside Velopack's execution stub (build agents' paths, none matching this PC's user, profile or computer name); and private-word #15 inside `Microsoft.Windows.SDK.NET.dll` and `System.Transactions.Local.dll`. Both files are byte-identical to their upstream NuGet packages, so the word is Microsoft's content. The first run blocked on them; the scanner now downgrades a finding to evidence only when the file is byte-identical to its upstream package file.
- [x] 5.3 Record the sizes: `Setup.exe`, the full `.nupkg` for each version, and the delta. Note which components dominate (the .NET runtime, the Windows App SDK) as input for [D]'s trimming and ReadyToRun decision.
  - Result (2026-11-05), Release, self-contained .NET 10 and Windows App SDK 2.6.5, win-x64, ReadyToRun off (the spike does not set it):
    - `Setup.exe` 102.0 MB; full `.nupkg` 94.9 MB per version; **delta 0.1.0 -> 0.1.1: 0.2 MB**; Setup reports 395.6 MB needed on disk for the install (app plus its package copy).
    - Publish folder 233.4 MB in 518 files: .NET runtime and BCL about 124 MB, Windows App SDK about 109 MB (of which `onnxruntime.dll` 22 MB and `DirectML.dll` 17.8 MB are Windows ML components the app does not use), Velopack 0.3 MB, the probe 2.8 MB. Largest single file: `Microsoft.Windows.SDK.NET.dll` 25.1 MB.
    - Input for [D]: try excluding the unused Windows ML runtime and trimming the Windows SDK projection; deltas keep updates small whatever the full size.

## 6. Update from a local feed (check g)

- [x] 6.1 Add the `check-update --feed <path|github> [--apply]` command and the "Check for updates" button (design D6), logging the request URL and headers it sends.
  - Result (2026-11-05): done. `check-update --feed <folder|github> [--apply]` uses `SimpleFileSource` or `GithubSource(repo, no token, prerelease: true, logging downloader)` with `ExplicitChannel = "s4-spike"`; it records the found version, base, deltas, sizes and every requested URL and header. The window's "Check for updates" button calls the same code. Run from the build output it skips (check d).
    - Driver fix found here: `Start-Process` joins arguments with spaces, so a feed path containing spaces was split; the driver now quotes such arguments.
- [x] 6.2 Run check (g):
  - install 0.1.0 from the local feed and write a marker
  - `check-update` finds 0.1.1 and plans the delta, and nothing is applied without `--apply`
  - `--apply` updates and restarts
  - Verify with the driver step: `version` = 0.1.1, the marker intact, the delta used (Velopack log), no leftover in the data root.
  - Result (2026-11-05): PASS.
    - With 0.1.0 installed and a marker written, `check-update` against the local feed found 0.1.1 with base 0.1.0 and one delta (0.2 MB, full package 99.5 MB), and applied nothing: the app still reported 0.1.0.
    - `--apply` downloaded the delta, rebuilt the full 0.1.1 package from it locally (zsdiff patches, about 5 s), replaced `Update.exe`, deleted the delta, then ran `Update.exe apply --waitPid <pid> ... -- version`, which waited for the app to exit, swapped `current\` and restarted the app with our `version` argument: it reported 0.1.1. The marker in the data root was intact; nothing was added to the data root apart from the probe's own results.
    - **Finding for [U] (guardrail 6):** Velopack's log messages contain full paths, including the Windows user name in `%LOCALAPPDATA%`. The real app must not forward them to its log as they are: filter the level, or replace the user-profile prefix before writing.

## 7. Update from GitHub pre-releases (check h)

- [x] 7.1 Before anything is published, reword the release rule in `CLAUDE.md` (Development workflow, Releases) and the matching text in `docs/roadmap.md` to: no `v*.*.*` tags before v1.0.0; the [S4] spike's temporary `s4-feed-*` pre-releases are the one recorded exception, deleted after the check. Verify that both files say the same.
  - Result (2026-11-05): done before any publish. `CLAUDE.md` (Development workflow, Releases) and `docs/roadmap.md` (Direction, Releases) now both say: no `v*.*.*` tags before v1.0.0; the one recorded exception is [S4]'s temporary `s4-feed-*` pre-releases, never `v*.*.*`, on their own Velopack channel, published and deleted only with the owner's approval at each step, and deleted right after the check.
- [x] 7.2 Ask the owner through AskUserQuestion to approve publishing pre-release `s4-feed-0.1.0` (the exact tag, title, channel `s4-spike` and the files). Only after a yes, run `vpk upload github` (design D6), with the token passed only through the child process's environment. Verify:
  - `gh release view s4-feed-0.1.0` shows it as a pre-release with the expected assets
  - the console output pasted into the notes contains no token
  - Result (2026-11-05): PASS, with the owner's approval through AskUserQuestion. Staged in a separate `GH` feed (0.1.0 packed alone), scanned (no blocking finding), then uploaded with the token only in `VPK_TOKEN` (vpk reads it from the environment; it never appeared in any output). `gh release view s4-feed-0.1.0`: pre-release, not draft, assets full `.nupkg` (99.5 MB), `Portable.zip`, `Setup.exe` (107.0 MB) and `releases.s4-spike.json`.
    - A first attempt with a short commit hash as `--targetCommitish` was rejected by GitHub (422, `target_commitish` invalid) before any release or tag was created; the retry used the full hash of `origin/main`. The tag points at `origin/main`, which does not contain the spike's source (owner informed in the approval question). The packages carry no third-party license notices yet ([D]).
- [x] 7.3 Ask again through AskUserQuestion for `s4-feed-0.1.1`, then publish it the same way. Verify the same way.
  - Result (2026-11-05): PASS, after a second, separate approval. 0.1.1 packed into the same `GH` feed (delta from 0.1.0: 0.2 MB), scanned (no blocking finding), uploaded the same way. `gh release view s4-feed-0.1.1`: pre-release, not draft, assets full `.nupkg`, delta `.nupkg` (177 KB), `Portable.zip`, `Setup.exe` and `releases.s4-spike.json` (542 bytes, listing both versions). No token in any output.
- [x] 7.4 Run check (h):
  - install 0.1.0, then `check-update --feed github` finds 0.1.1 through the anonymous API with the pre-release and channel filters
  - `--apply` updates
  - Verify with the driver step: `version` = 0.1.1, the marker intact, and the logged request has no token and no user or machine data (guardrail 2).
  - Result (2026-11-05): PASS. Installed 0.1.0 from the `Setup.exe` downloaded from the published `s4-feed-0.1.0` release, wrote a marker, then:
    - `check-update --feed github` found 0.1.1 with one delta (177 KB) through `GithubSource` with no token, `prerelease: true` and channel `s4-spike`.
    - Requests (all recorded): `GET https://api.github.com/repos/<owner>/mordWare/releases?per_page=10&page=1` with `Accept: application/vnd.github.v3+json`, then `releases.s4-spike.json` of each release and the delta, from `github.com/<owner>/mordWare/releases/download/...`, with `Accept: application/octet-stream`. User-Agent `Velopack/1.2.161` on every request; no `Authorization` header, no user, machine or app-usage data (guardrail 2).
    - `--apply` downloaded the delta, rebuilt 0.1.1 locally, and `Update.exe apply` restarted the app on 0.1.1; the marker was intact. The installed spike was then uninstalled.
    - For [U]: one check costs one GitHub API request (the release list, 10 per page) plus one small file per listed release with the channel's asset, well inside the anonymous limit of 60 API requests per hour per IP.
- [x] 7.5 Optional, owner-driven (design D8): while 0.1.0 is published, the owner downloads its `Setup.exe` with the browser and runs it, and records what Windows shows. If the owner declines, record NOT VERIFIED (manual).
  - Result (2026-11-05, done by the owner): PARTLY VERIFIED.
    - The owner downloaded the 0.1.0 `Setup.exe` from the release page with the browser. The **browser** asked whether to keep or discard the file (its own download-reputation check for an unsigned, rarely downloaded executable). After "keep", the installer ran with **no Windows SmartScreen prompt** and installed normally.
    - Read-only checks afterwards: the downloaded file has **no Mark-of-the-Web** (`Zone.Identifier` stream absent), which explains why SmartScreen did not evaluate it on this PC. On PCs where the browser writes Mark-of-the-Web, the documented "Windows protected your PC" screen is expected (see 8.1). So the SmartScreen screen itself is NOT VERIFIED here.
    - The normal (non-silent) install started the app once after installing, and Velopack's first-run hook fired (`firstrun.json`, version 0.1.0), unlike the silent install (3.1).
    - The downloaded file stays in the owner's Downloads folder for the owner to delete; the installed spike is removed by the cleanup (9.1).
- [x] 7.6 Ask the owner through AskUserQuestion to approve deleting both releases and tags. Only after a yes, run `gh release delete s4-feed-<version> --cleanup-tag --yes` for each. Verify:
  - `gh release list` and `git ls-remote --tags origin` show no `s4-feed-*`
  - `git tag -l "s4-feed-*"` is empty locally
  - Result (2026-11-05): PASS, after the owner's approval through AskUserQuestion. `gh release delete s4-feed-0.1.0 --cleanup-tag --yes` and the same for `s4-feed-0.1.1`: `gh release list` shows no release, `git ls-remote --tags origin` shows no tag, and `git tag -l "s4-feed-*"` is empty locally (after `git fetch --prune --prune-tags`). The two pre-releases were public for about 20 minutes.

## 8. First run of an unsigned installer (check f)

- [x] 8.1 Read, without changing anything, the Smart App Control state (`VerifiedAndReputablePolicyState`) and the Mark-of-the-Web of a downloaded file if 7.5 ran. From Microsoft's public documentation, record how SmartScreen and Smart App Control treat an unsigned per-user installer and app, citing public URLs.
  - Verify: the notes give PASS / NOT VERIFIED per item, with the source.
  - If Smart App Control would block mordWare with no override, flag it in the roadmap as a decision the owner must take before v1.0.0.
  - Result (2026-11-05):
    - **Smart App Control on this PC: off** (`VerifiedAndReputablePolicyState` = 0, read only; nothing changed). So it could not be observed here: NOT VERIFIED (by measurement), documented instead.
    - **Mark-of-the-Web:** the browser download used in 7.5 carried none (`Zone.Identifier` absent), so SmartScreen did not evaluate it here: NOT VERIFIED (by measurement).
    - **From Microsoft's public documentation:**
      - SmartScreen (https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation): an unsigned file shows "Windows protected your PC", and the user must choose "Run anyway"; enterprise policy can prevent continuing. Unsigned files start at zero reputation with every new version, and no reputation carries over between versions. Reputation builds only through download volume ("several weeks and hundreds of clean installs"); there is no submission for consumer endpoints.
      - Smart App Control (same page, and https://support.microsoft.com/en-us/windows/security/threat-malware-protection/smart-app-control-frequently-asked-questions): on Windows 11 it "may supersede SmartScreen" and "will block execution of unsigned files unless the file has a positive reputation"; its checks apply to all executable files, not only downloads. "There is currently no way to bypass Smart App Control protection for individual apps"; the user's only options are to turn it off or to ask the developer to sign. The FAQ now says recent Windows updates allow turning it back on without a clean install.
    - **Conclusion, flagged in the roadmap as an owner decision before v1.0.0:** on a PC with Smart App Control on, an unsigned mordWare (its `Setup.exe`, its app and `Update.exe`) will most likely be blocked with no per-app override, until the files gain reputation. Every unsigned release starts again at zero. That is a direct risk to the "no code signing" decision.
- [x] 8.2 Draft the user-facing "first install" wording (SmartScreen "More info" -> "Run anyway"; what to do when Smart App Control is on) in the [D] notes of `docs/roadmap.md`, for [D]'s README. Verify that it is in US English and contains nothing machine-specific.
  - Result (2026-11-05): done. The [D] notes in `docs/roadmap.md` carry a draft "first install" wording for the README: the browser's keep-or-discard prompt, SmartScreen's "More info" -> "Run anyway", what Smart App Control means (no per-app exception), no admin rights, and where data stays after uninstall. It is US English, uses `%LOCALAPPDATA%` placeholders and has nothing machine-specific.

## 9. Outcome, docs and cleanup

- [x] 9.1 Run `run-checks.ps1 -CleanupOnly`. Verify:
  - no `MordWare.Spike.S4*` install folders, uninstall entries or Start menu shortcuts remain
  - no `mordWare S4 spike*` data roots or local feed remain
  - `MordWare.Dev` is still registered as before the spike
  - Result (2026-11-05): PASS. `run-checks.ps1 -CleanupOnly` uninstalled the spike the owner installed in 7.5 and removed the rest. Afterwards: no `MordWare.Spike.S4*` install folders, uninstall entries or Start menu shortcuts; no `mordWare S4 spike*` data roots under `%LOCALAPPDATA%`; no feed, publish or scan folders under `%TEMP%`; no probe running. `MordWare.Dev` is still registered as before the spike, and PROD (`MordWare`) is still not installed. Left for the owner: the `Setup.exe` in their Downloads folder (7.5) and their empty picker test folder (4.1).
- [x] 9.2 Update `docs/roadmap.md`:
  - for [S4]: `done`, with conditions, the outcome table (a)-(j) and its findings
  - for [D]: the adopted settings (unpackaged, self-contained, x64 only, the entry point, packIds `MordWare` / `MordWare.Dev`, the channel, deterministic build plus binary scan, sizes, the first-install wording)
  - for [1] Foundation: the data roots `%LOCALAPPDATA%\mordWare Data` and `mordWare Data (DEV)`; settings in a file because `ApplicationData` and `LocalSettings` do not exist unpackaged
  - for [U]: the update API, `GithubSource` with no token, default channel, no pre-releases; the rate limit
  - Verify that the table row and every touched section agree.
  - Result (2026-11-05): done. The table row and the [S4] section say `done`, with conditions, decisions and the outcome table (a)-(j). [D] has the platform decision (x64 only), the adopted project settings, entry point, edition, language, Velopack names, install facts, sizes, binary scan and upload notes, the Smart App Control owner decision and the first-install wording. [1] Foundation has the data roots and settings in a file. [U] has the update API, what a check sends, the rate limit and the log rule. The [S3] live-database line points at the new location.
- [x] 9.3 Update the "Delivery (planned)" section of `docs/architecture.md` with the decided data roots and installer, and confirm that `CLAUDE.md` describes the delivery model accurately. Verify that `README.md` needs no change, or update it.
  - Result (2026-11-05): done. `docs/architecture.md` "Delivery (planned)": Velopack confirmed (per user, no admin, `%LOCALAPPDATA%\<packId>\`, deltas, x64 only), the unsigned first run (browser, SmartScreen, Smart App Control and its open owner decision), and the decided data roots `mordWare Data` / `mordWare Data (DEV)`. `README.md` needed one change: its unsigned-installer sentence now also mentions the browser warning and Smart App Control. `CLAUDE.md` still describes the delivery model accurately (unpackaged, per-user installer on GitHub Releases, no signing, update check), and its release rule carries the recorded tag exception (7.1).
- [x] 9.4 Run `scripts/check-public-safety.ps1 -All` with the local deny list present. Verify that it exits 0 before archive.
  - Result (2026-11-05): PASS. `scripts/check-public-safety.ps1 -All` with the local deny list present: "all check passed", both on the tracked files and with the 20 new `spikes/S4.Installer` files temporarily marked intent-to-add (then unmarked).

## Workflow follow-up

- `/opsx:archive` once every task above is done: mark [S4] done in the roadmap (if not already) and make one Conventional Commit on `main` authored as the public project identity; push only if the owner asks.
