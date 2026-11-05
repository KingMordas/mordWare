# mordWare

Your personal household assistant for Windows 11: vehicles, houses and finance, managed entirely on your own PC. There is no account, no server and no tracking: your data stays on your computer, encrypted at rest (from the Foundation milestone onward).

> **Status: early development.** The app opens an empty window; nothing is released yet. The first release will be **v1.0.0**, once the Vehicles, Houses and Finance modules are complete. See [`docs/roadmap.md`](docs/roadmap.md).

## A free hobby project, provided as is

mordWare is free and open source under the [MIT license](LICENSE). It is a personal project developed in spare time and shared with anyone who finds it useful.

It is provided **as is, without warranty of any kind**, and without guaranteed support, response times or a fixed schedule. Keep your own backups.

## Getting mordWare

Releases will be published on [GitHub Releases](https://github.com/KingMordas/mordWare/releases) as a Windows installer that installs mordWare or upgrades an existing installation, and the app will offer new versions when they appear. The installer is not code-signed, so Windows SmartScreen may warn you the first time you run it.

## Building from source

### Requirements

- Windows 11 (22H2 / build 22621 or later). Today the DEV edition runs as a development package, which needs **Developer Mode** (Settings > System > For developers).
- .NET SDK 10.0.403 or a later 10.0 feature band (see `global.json`).
- Optional: Visual Studio 2026 with the WinUI application development workload.
- To commit: [gitleaks](https://github.com/gitleaks/gitleaks) (MIT), for example `winget install Gitleaks.Gitleaks`.

### Build, test, run

```powershell
.\scripts\run-dev.ps1                       # build, deploy and launch "mordWare (DEV)"
.\scripts\run-dev.ps1 -NoBuild              # launch the already-deployed DEV edition
.\scripts\run-dev.ps1 -Reset                # remove DEV (and its data!) first, then rebuild; asks to confirm
dotnet build MordWare.slnx                  # build everything (Debug = DEV edition)
dotnet test --solution MordWare.slnx        # all tests (unit + architecture)
```

`run-dev.ps1` runs in Windows PowerShell 5.1 and PowerShell 7. It checks Developer Mode and the .NET SDK, and it never builds Release or touches the PROD edition. If script execution is blocked, run it as `powershell -ExecutionPolicy Bypass -File .\scripts\run-dev.ps1`.

Debug builds always produce the **DEV** edition, "mordWare (DEV)", with an orange DEV badge. It has its own data folder and can be installed next to the real **PROD** edition, "mordWare", which only `Release` builds produce. The two never share data. See [`docs/architecture.md`](docs/architecture.md#editions-dev-and-prod).

### Before your first commit

This repository is public, so every commit is checked for secrets, personal data and machine-specific details ([`docs/public-safety.md`](docs/public-safety.md)):

```powershell
git config core.hooksPath .githooks                       # enable the pre-commit and commit-msg checks
.\scripts\check-public-safety.ps1 -SelfTest               # confirm the checks work on your machine
```

Optionally, list your own private words (your name, e-mail, user name) in `.public-safety.local.txt`, one per line. The file is gitignored and the checks refuse any commit that contains them.

## Documentation

- [`CLAUDE.md`](CLAUDE.md): project rules (guardrails, definition of done, workflow).
- [`docs/architecture.md`](docs/architecture.md): solution layout, modules, editions, localization, CI.
- [`docs/roadmap.md`](docs/roadmap.md): the plan, and what's next.
- [`docs/public-safety.md`](docs/public-safety.md): what may never be published, and how it is checked.
- [`CHANGELOG.md`](CHANGELOG.md): user-visible changes.
