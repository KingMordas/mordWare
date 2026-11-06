# mordWare

Your personal household assistant for Windows 11: vehicles, houses and finance, managed entirely on your own PC. There is no account, no server and no tracking: your data stays on your computer, encrypted at rest (from the Foundation milestone onward).

> **Status: early development.** The app opens an empty window; nothing is released yet. The first release will be **v1.0.0**, once the Vehicles, Houses and Finance modules are complete. See [`docs/roadmap.md`](docs/roadmap.md).

## A free hobby project, provided as is

mordWare is free and open source. It is a personal project developed in spare time and shared with anyone who finds it useful.

It is provided **as is, without warranty of any kind**, and without guaranteed support, response times or a fixed schedule. Keep your own backups.

### License

Copyright (C) 2026 KingMordas.

mordWare is licensed under the **GNU General Public License v3.0 only** (SPDX: `GPL-3.0-only`); see [`LICENSE`](LICENSE). You may use, study, share and modify it under those terms. If you distribute it, modified or not, you must do so under the same license, with the complete source code. This program comes with ABSOLUTELY NO WARRANTY, to the extent permitted by applicable law.

mordWare runs on Microsoft's Windows App SDK and the Windows SDK projection for .NET, which Microsoft distributes under its own license terms, not under an open-source license. So the license comes with **one additional permission under GPL-3.0 section 7** for exactly that platform runtime: you may combine mordWare with it and distribute the result. See [`LICENSE-EXCEPTION.txt`](LICENSE-EXCEPTION.txt); it covers nothing else. Every bundled third-party component and its license is listed in [`THIRD-PARTY-NOTICES.txt`](THIRD-PARTY-NOTICES.txt), which also ships with the app.

### Privacy

mordWare itself sends no data: no telemetry, no analytics, no tracking, no account. Your data stays on your PC. The Microsoft platform runtime bundled with the app (the Windows App SDK) may record usage data to Windows, which handles it according to your Windows diagnostic-data setting (Settings > Privacy & security > Diagnostics & feedback).

### Contributions

mordWare does **not accept contributions**: pull requests and issues are closed to outside contributors, and only the author changes this repository. You are welcome to fork it under the terms of the license.

## Getting mordWare

There is no release yet: the first one will be **v1.0.0**. It will be published on [GitHub Releases](https://github.com/KingMordas/mordWare/releases) as a Windows installer (x64) that installs mordWare or upgrades an existing installation, and the app will offer new versions when they appear. Every release links the source code of exactly that version.

- **Download mordWare only from its GitHub Releases page.**
- **It is free and not code-signed**, so your browser and Windows may warn you the first time:
  - Your browser may say the file is not commonly downloaded: choose **Keep**.
  - If Windows shows "Windows protected your PC", select **More info**, check that it is the mordWare installer you downloaded from GitHub, then select **Run anyway**.
  - If Windows says that Smart App Control blocked the app: Windows allows no exception for a single app, so mordWare cannot run on this PC while Smart App Control is on. _(Provisional: this depends on a decision before v1.0.0.)_
- **No administrator rights are needed.** mordWare installs for your Windows account only, adds a Start menu entry and appears in Settings > Apps > Installed apps.
- **Your data stays when you uninstall.** Uninstalling removes the program, its Start menu entry and its installed-apps entry, and keeps your data in `%LOCALAPPDATA%\mordWare Data` (and your backups). To remove your data completely, delete that folder after uninstalling.

## Building from source

### Requirements

- Windows 11 (22H2 / build 22621 or later), x64.
- .NET SDK 10.0.403 or a later 10.0 feature band (see `global.json`).
- To pack an installer: PowerShell 7, and `dotnet tool restore` once (it installs the pinned `vpk` packer).
- Optional: Visual Studio 2026 with the WinUI application development workload.
- To commit: [gitleaks](https://github.com/gitleaks/gitleaks) (MIT), for example `winget install Gitleaks.Gitleaks`.

### Build, test, run

```powershell
.\scripts\run-dev.ps1                       # build and launch "mordWare (DEV)" from the build output
.\scripts\run-dev.ps1 -NoBuild              # launch the existing DEV build
.\scripts\run-dev.ps1 -Reset                # delete the DEV data folder first, then build and launch; asks to confirm
dotnet build MordWare.slnx                  # build everything (Debug = DEV edition)
dotnet test --solution MordWare.slnx        # all tests (unit + architecture)
```

`run-dev.ps1` runs in Windows PowerShell 5.1 and PowerShell 7. It never builds Release or touches the PROD edition or its data. If script execution is blocked, run it as `powershell -ExecutionPolicy Bypass -File .\scripts\run-dev.ps1`.

Debug builds always produce the **DEV** edition, "mordWare (DEV)", with an orange DEV badge, its own icon and its own data folder, `%LOCALAPPDATA%\mordWare Data (DEV)`. It runs from the build output and is never published. Only `Release` builds produce the real **PROD** edition, "mordWare", which users install and which keeps its data in `%LOCALAPPDATA%\mordWare Data`. The two never share data. See [`docs/architecture.md`](docs/architecture.md#editions-dev-and-prod).

To check the installer locally, pack the DEV edition (never install a PROD build for a check):

```powershell
.\scripts\pack.ps1 -Edition Dev -Version 0.0.1   # artifacts\pack\Dev\releases\MordWare.Dev-win-Setup.exe, scanned
```

### Before your first commit

This repository is public, so every commit is checked for secrets, personal data and machine-specific details ([`docs/public-safety.md`](docs/public-safety.md)):

```powershell
git config core.hooksPath .githooks                       # enable the pre-commit and commit-msg checks
.\scripts\check-public-safety.ps1 -SelfTest               # confirm the checks work on your machine
```

Optionally, list your own private words (your name, e-mail, user name) in `.public-safety.local.txt`, one per line. The file is gitignored and the checks refuse any commit that contains them.

## Documentation

- [`CLAUDE.md`](CLAUDE.md): project rules (guardrails, definition of done, workflow).
- [`docs/architecture.md`](docs/architecture.md): solution layout, modules, editions, localization, packing, releases, CI.
- [`docs/roadmap.md`](docs/roadmap.md): the plan, and what's next.
- [`docs/public-safety.md`](docs/public-safety.md): what may never be published, and how it is checked.
- [`CHANGELOG.md`](CHANGELOG.md): user-visible changes.
