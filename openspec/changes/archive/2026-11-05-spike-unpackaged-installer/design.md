# Design

## Context

- **The app today** depends on its MSIX package identity in three places, and each one throws `InvalidOperationException` or a COM error without that identity:
  - `App.OnLaunched` sets `Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride`.
  - `AppHost.Create` resolves the edition from `Package.Current.Id.Name`.
  - `PackagedAppPaths` builds `Data\` and `Logs\` under `ApplicationData.Current.LocalFolder`.
- **Packaging today:**
  - `MordWare.App.csproj` sets `EnableMsixTooling`, picks `Package.appxmanifest` or `Package.Dev.appxmanifest` by configuration, and references `Microsoft.Windows.SDK.BuildTools.WinApp` for `dotnet run`.
  - `MordWare.slnx` maps the App to x86.
  - Windows App SDK 2.6.5 and .NET SDK 10.0.403 (`global.json`).
- **Facts carried over from [S3]:**
  - Backups belong in a user-chosen folder (default under Documents) and survive anything that happens to the app.
  - DPAPI CurrentUser works for the data key.
  - An unsigned MSIX with an executable cannot be installed at all, which is why mordWare goes unpackaged.
- **This PC:**
  - Windows build 26300, x64.
  - Windows Sandbox is not enabled. It stays that way: no Windows setting or optional feature is changed (proposal, Why).
  - `gh` is authenticated as the project identity.
- **No GitHub Release exists, and no tag either.** CI (`ci.yml`) runs only on pushes to `main` and on pull requests, never on tags.

See proposal.md for the decisions the spike validates.

## Goals / Non-Goals

**Goals:**
- Each check, (a) to (j), ends in PASS, FAIL or NOT VERIFIED, with evidence: paths seen from outside the app, file listings, exit codes, sizes, and the HTTP request seen by the probe. A FAIL is a valid result and is recorded, with the fallback it points to.
- [D] can unpackage the app, build the installer and write the release workflow from the recorded settings without a new decision. Foundation and [U] get their data root and update API the same way.
- At the end, nothing the spike created remains:
  - on this PC: spike installs, spike data roots, spike shortcuts and uninstall entries, and the local feed
  - on GitHub: the `s4-feed-*` releases and tags

**Non-Goals:**
- Production-quality UI, localization or error handling. The probe has one window and a handful of buttons, in English, except for the two resource strings that check (d) needs in both languages.
- The real update UX (a prompt, a Settings switch): that is [U].
- The release workflow, `THIRD-PARTY-NOTICES`, the GPL source offer: that is [D].
- Measuring Smart App Control or SmartScreen by changing any setting, or by enabling Windows Sandbox or a VM feature.

## Decisions

### D1. An unpackaged probe app with its own packIds

- `spikes/S4.Installer/S4.Installer.csproj`:
  - WinUI 3, `WindowsPackageType=None`, `WindowsAppSDKSelfContained=true`, `SelfContained=true`, `RuntimeIdentifier=win-x64`, `Platforms=x64`.
  - Not in `MordWare.slnx`; it references nothing in `src/`.
- **Why self-contained .NET and Windows App SDK,** rather than Velopack's runtime bootstrapper (`--framework`) or the Windows App SDK framework package:
  - The framework package needs an MSIX runtime install, which is exactly what mordWare is leaving behind.
  - Bundling everything makes the installer the only thing a user runs, at the cost of size.
  - The size is measured in (g). Delta updates keep later downloads small.
- **Edition (D4) by build:**
  - Debug -> packId `MordWare.Spike.S4.Dev`, title "mordWare S4 spike (DEV)".
  - Release -> packId `MordWare.Spike.S4`, title "mordWare S4 spike".
  - The packId is passed to `vpk pack` by the driver from the same configuration switch, so the build and the package cannot disagree.
- **Spike data roots** (the D3 layout, with throwaway names):
  - `%LOCALAPPDATA%\mordWare S4 spike Data\` (PROD-like)
  - `%LOCALAPPDATA%\mordWare S4 spike Data (DEV)\` (DEV-like)
  - Results go to `%LOCALAPPDATA%\mordWare S4 spike Data*\results\<command>.json`. The driver reads them from outside.
- **Alternatives considered:**
  - A packaged probe with sparse identity: rejected, because sparse packages need signing.
  - Reusing the real app: rejected, because a spike never touches `src/`.

### D2. Entry point: Velopack first, then WinUI

- The probe defines `DISABLE_XAML_GENERATED_MAIN` and its own `Program.Main`, `[STAThread]`:
  1. `VelopackApp.Build().Run()` handles Velopack's install, update and uninstall hooks and returns quickly. Nothing it does touches the data root.
  2. Command dispatch (D5), if there are arguments.
  3. Otherwise `WinRT.ComWrappersSupport.InitializeComWrappers()` and `Application.Start(...)`, the same steps the generated `Main` performs.
- Run from the build output, `UpdateManager.IsInstalled` is false, so the probe skips update checks without an error. That is the "DEV from build output" path, checked in (d).
- **Alternative considered:** calling Velopack from `App.OnLaunched`. Rejected, because Velopack documents that its hooks must run before any UI and may exit the process.

### D3. Folder layout under test

```
 %LOCALAPPDATA%\
   MordWare.Spike.S4\               <- Velopack (PROD-like): Update.exe, current\, packages\
                                       uninstall deletes ALL of it
   MordWare.Spike.S4.Dev\           <- Velopack (DEV-like)
   mordWare S4 spike Data\          <- ours (PROD-like): marker, picker.json, results\
   mordWare S4 spike Data (DEV)\    <- ours (DEV-like)
```

- The real names that [D] and Foundation adopt follow the same shape: packIds `MordWare` and `MordWare.Dev`, and data roots `mordWare Data` and `mordWare Data (DEV)`.
- **The trap check (b):** a marker written to `%LOCALAPPDATA%\mordware.spike.s4\trap.txt` must be gone after uninstall. The lowercase name lands inside Velopack's folder, because Windows paths ignore case. This shows why the data root must have a name distinct from every packId.
- **No `ApplicationData` and no `LocalSettings`,** because neither exists unpackaged. The picker path (e) is stored in a small JSON file in the spike data root. That is the pattern Foundation's settings will need too.

### D4. Edition and language without package identity

- **Edition:**
  - A build constant: `DefineConstants` gets `MORDWARE_PROD` only when `Configuration == Release`, and code reads it through a single `#if`.
  - The probe reports its edition, packId, executable path and data root in `where.json`.
- **Language:**
  - The probe sets `Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride`, the Windows App SDK version, which supports unpackaged apps, before the first window.
  - It then reads two `.resw` strings through `ResourceLoader`, in en-US and in it-IT runs.
  - It also records that the `Windows.Globalization` version throws unpackaged. That is evidence for [D]'s change to `App.ApplyAppLanguage`.
  - `CultureInfo` is set as the app does today.
- **Alternative:** an environment variable or command-line flag for the edition. Rejected, because guardrail 7 forbids a runtime switch.

### D5. Probe commands and driver

- **Probe commands** (each writes `results\<command>.json` and exits):
  - `where`
  - `write-marker`
  - `read-marker`
  - `lang <tag>`
  - `check-update --feed <local path | github> [--apply]`
  - `version`
- **The window** has "Pick folder", "Write test file" and "Check for updates". It is used for (e) and for the manual part of (f).
- **`spikes/S4.Installer/run-checks.ps1`, the driver:**
  - `Pack -Version -Configuration`:
    - `dotnet publish` (Release or Debug, win-x64, deterministic, `PathMap`, no PDBs in the package)
    - then `vpk pack --packId --packVersion --packDir --mainExe --channel s4-spike` into `%TEMP%\mordWare S4 spike feed\`
  - `Install` runs `Setup.exe` silently. `Uninstall` runs Velopack's own uninstall (`Update.exe --uninstall`).
  - `Check-a` to `Check-j` print PASS / FAIL / NOT VERIFIED with evidence.
  - `-CleanupOnly` removes every spike install, spike data root, the local feed and the probe's results.
  - **A guard:** every install, uninstall or delete helper refuses any packId or folder that does not contain `Spike.S4` / `S4 spike`. So `MordWare`, `MordWare.Dev` and `mordWare Data*` are unreachable by construction.
- **`spikes/S4.Installer/scan-package.ps1`** (D7).
- **Tool pinning:**
  - Velopack is pinned in `spikes/Directory.Packages.props`.
  - `vpk` is pinned in a .NET local tool manifest at `spikes/S4.Installer/.config/dotnet-tools.json`, so nothing is installed globally on the PC.
  - Both use the latest stable version available when the spike is applied, with its license recorded.

### D6. Update sources and the GitHub pre-releases

- **Local feed (g):**
  - Pack 0.1.0 and 0.1.1 into the same feed folder; `vpk` produces a delta for 0.1.1.
  - Install 0.1.0 and write a marker.
  - Run `check-update --feed <folder>` and verify it finds 0.1.1 and chooses the delta.
  - Run it again with `--apply`; Velopack downloads, applies and restarts.
  - Verify `version` = 0.1.1 and the marker intact.
  - Record the full and delta `.nupkg` sizes and the `Setup.exe` size.
- **GitHub (h):**
  - **Source:** `GithubSource(<public repository URL>, accessToken: null, prerelease: true)`, with `UpdateOptions.ExplicitChannel = "s4-spike"`. The real app will use the default channel and `prerelease: false`, so it can never see these releases.
  - **Upload:**
    - `vpk upload github --repoUrl <public URL> --tag s4-feed-<version> --releaseName "S4 spike feed <version> (temporary, not a mordWare release)" --pre --publish --channel s4-spike`
    - The token comes from `gh auth token` into an environment variable of that one process, never printed, logged or written to disk.
  - **Order:** scan (D7), then the owner approves the upload of 0.1.0, then of 0.1.1. Then install 0.1.0 from the local `Setup.exe` and run `check-update --feed github` (and `--apply`).
  - **What the request carries:** a local proxy is not used, because that would mean changing settings. The probe logs the request URL and headers it sends through Velopack's logger, or `HttpClient` diagnostics. They must contain no token, no user or machine data, only the GitHub API URL and a generic user agent.
  - **Cleanup:** after the owner approves, `gh release delete s4-feed-<version> --cleanup-tag --yes` for both. Then verify `gh release list` and `git ls-remote --tags origin` show no `s4-feed-*`, and delete any local tag fetched meanwhile.
  - **Approvals:** every upload and delete step is asked for separately through AskUserQuestion, naming the exact tag and action.
- **Why not a throwaway repository or a private one:** the owner refused a throwaway public repository. A private repository needs a token, so it would not test the anonymous path real users take.

### D7. Binary public-safety scan

- **Build side:** `Deterministic=true`, `ContinuousIntegrationBuild=true`, `PathMap=$(MSBuildProjectDirectory)=/_/`, and `DebugType=none` for the packed build.
- **`scan-package.ps1 -Path <nupkg or Setup.exe or folder>`:**
  - It expands every `.nupkg` (a zip) into a temp folder.
  - It extracts printable strings, both ASCII and UTF-16LE, from every file, and matches them against copies of the rules in `scripts/check-public-safety.ps1`: user-folder path, package-family hash and e-mail address. It also checks the lines of the private deny list `.public-safety.local.txt`, read locally and never printed in full; a finding shows the file and the matched rule.
  - `Setup.exe` embeds the package, so the scan covers it through the same string pass. The spike also checks whether it can find the embedded payload.
- Findings that come from Microsoft's or Velopack's own binaries, such as build-agent paths like `D:\a\_work\`, are not user-folder paths. Their rule does not match them, and they are listed as third-party in the evidence.
- A finding in our own files blocks the upload.
- **Why a spike copy and not a new mode of `check-public-safety.ps1`:** a spike never changes production tooling. [D] decides whether to fold it in.

### D8. First run of an unsigned installer (f), without touching settings

- **Read-only state:**
  - Smart App Control's state from `HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy` (`VerifiedAndReputablePolicyState`), read only.
  - Whether the downloaded file carries Mark-of-the-Web (`Get-Item -Stream Zone.Identifier`).
- **Optional manual step:** with the owner's approval, while the 0.1.0 pre-release exists, the owner downloads `Setup.exe` with the browser and runs it. They record what Windows shows (the SmartScreen "Windows protected your PC" screen, "More info", "Run anyway"), with no screenshot unless it is checked pixel by pixel (public-safety checklist 7). If Smart App Control is on and blocks the run, that is recorded and nothing is changed.
- **Documentation:**
  - From Microsoft's public documentation: how SmartScreen reputation treats unsigned downloads, and whether Smart App Control blocks unsigned apps with no per-app override (to be confirmed from the documentation, not assumed).
  - Draft user-facing wording for [D]'s README, recorded in the roadmap notes.
- **Outcome when Smart App Control blocks unsigned apps:** record it as a risk to the "no code signing" decision, and raise it with the owner before v1.0.0. The spike does not decide it.

## Risks / Trade-offs

- **Velopack and WinUI self-contained interplay (deltas, the `current\` swap while files are locked)** -> (g) runs a real update. If it fails, the fallback (Inno Setup plus a custom GitHub check) is recorded with what it would cost.
- **Smart App Control can block every unsigned executable on PCs that have it on** -> it cannot be measured without changing settings. Documented from Microsoft's documentation and escalated as a decision for the owner (D8).
- **Pre-releases are public while they exist; watchers may be notified; caches may keep copies** -> the binaries are scanned first (D7), the titles say "temporary, not a mordWare release", they are deleted right after (h), and the owner approves each step.
- **The token could leak through a log or the console** -> it lives only in one child process's environment. The driver never echoes it, and `vpk`'s output is checked for it before it is pasted into task notes.
- **A helper touching the real editions by mistake** -> the name guard in the driver (D5). Neither real edition is MSIX-installed by this spike, and `MordWare.Dev` stays as it is.
- **The self-contained package is large (expected to be over 100 MB)** -> it is measured. Delta updates are the mitigation. Trimming or ReadyToRun choices are left to [D] with the numbers.
- **The GitHub API rate limit (60 requests per hour per IP, anonymous)** -> one check per startup is far below it. Recorded for [U].

## Migration Plan

Nothing is deployed. Rollback is `run-checks.ps1 -CleanupOnly` plus the GitHub cleanup in D6. The spike folder stays as reference code until [D] adopts it, and [D] then deletes it (the `spikes/` lifetime rule).

## Open Questions

- The exact Velopack and `vpk` versions, and whether `vpk upload github` needs a flag to keep a release as pre-release when publishing. Both are answered while applying and recorded; neither changes the approach.
