# Proposal

## Why

Uninstalling an MSIX package deletes its LocalState, and so does Settings > Apps > Reset. mordWare plans to keep its live, encrypted database in LocalState, so the backups are the only copy of the owner's data that outlives the package. Foundation will build backup and restore on a few claims nobody has checked yet:

- which folders a full-trust packaged app writes to for real, and which ones Windows silently redirects into the package
- that those real folders survive uninstall and reset
- that LocalState survives an update
- that the [S1] key chain can rebuild a database from a backup alone, after a reinstall

Each wrong claim would cost real data, so they are proven in throwaway code before Foundation.

The 2026-11-04 explore of roadmap item [S3] settled these decisions; the spike validates them:

- **Live database:** package LocalState, per edition. It never lives in a synced folder.
- **Backups:** a folder the user chooses, outside the package and outside AppData. The default is `Documents\mordWare Backups` (PROD) or `Documents\mordWare Backups (DEV)`, confirmed by the user on first run, never chosen silently. If Documents is synced by OneDrive, backups are synced too; that is acceptable because backups are encrypted and written whole.
- **Every restore needs the recovery key.** A backup carries only the recovery-wrapped data key in its header, never a DPAPI-wrapped copy. So a backup copied off the PC or the cloud cannot be opened by someone who only has the Windows account.
- **Key storage** (the open question from [S1]):
  - The DPAPI-wrapped data key is a key file next to the database in LocalState. It is lost with the database, which is correct.
  - The recovery-wrapped data key goes in each backup's header, so every backup is self-contained.
- **When backups run:**
  - on the first launch of the day
  - before every migration
  - manually ("Back up now")
  - Never on exit, because closing the app does not reliably leave time to finish one.
  - The retention of 7 daily + 4 weekly + 6 monthly stays as proposed for Foundation.

## What Changes

- **A throwaway spike, `spikes/S3.Storage`.** It is a minimal packaged WinUI 3 probe app with its own package identity, `MordWare.Spike.S3`, never `MordWare` or `MordWare.Dev`.
  - A PowerShell driver script installs, runs, updates, resets and uninstalls it, and checks the file system from outside the package.
  - Like S1 and S2, it is not in `MordWare.slnx` and is referenced by nothing.
- **What the spike proves or disproves** (criteria in design.md):
  - **(a)** A backup written to a Documents folder survives uninstall.
  - **(b)** A write to `%LOCALAPPDATA%\<folder>` from the package is redirected into the package and deleted on uninstall. This is the trap that rules AppData out for backups.
  - **(c)** LocalState survives a package update, and a re-deploy the way `scripts/run-dev.ps1` deploys DEV (`dotnet run`, registering from the build output). This is reproduced on the spike identity, so DEV is never touched.
  - **(d)** A reset wipes LocalState and leaves the backup folder alone.
  - **(e)** The WinUI 3 folder picker works in a full-trust package, and the chosen path is remembered and still writable after a restart.
  - **(f)** Backups behave correctly in a OneDrive-synced folder, including a cloud-only file being downloaded on restore. If OneDrive is not set up on this PC, this is recorded as unverified.
  - **(g)** A backup is written atomically: the SQLite backup API writes a `.tmp` file, which is then renamed. An interrupted backup never leaves a valid-looking backup file.
  - **(h)** The DEV and PROD default backup folders never cross, and each backup's header records its edition.
  - **(i)** End to end: create an encrypted database in LocalState, back it up with a header, uninstall, reinstall, then restore with the recovery key alone and re-wrap the key with DPAPI. A backup whose header names the other edition is refused.
- **Recorded outcome:**
  - pass or fail per criterion in the task notes
  - `docs/roadmap.md`: [S3] marked done with its notes rewritten; Foundation's notes get the folder layout, the backup header draft and the restore flow; the [S1] key-storage question is closed
  - `docs/architecture.md`: the `spikes/` paragraph names S3, and the Editions section records the storage facts

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. A spike changes no behavior of the app, so this change sets `skip_specs: true`. Backup, restore and the first-run backup-folder choice get their specs in Foundation, where they become real.

## Scope

**In:**
- The `spikes/S3.Storage` probe app and its driver script, under the spike identity `MordWare.Spike.S3`.
- Checks (a) to (i) above, run on this PC.
- A draft backup header: magic, format version, edition, schema version and recovery-wrapped data key. Only enough to prove (h) and (i); Foundation finalizes the format.
- Own copies of the [S1] key-wrap and recovery-key helpers, as S2 copied its helpers: no spike references another.
- Docs: `docs/roadmap.md` and `docs/architecture.md`.

**Out:**
- **Left to Foundation:**
  - Any production code in `src/` or `tests/`, including adding a backup folder to `IAppPaths`.
  - Any change to `MordWare.slnx`, the app manifests, the root `Directory.Packages.props` or the architecture tests.
  - The real backup service, scheduling, retention and the first-run UI with its localized texts.
  - The recovery-key check symbol.
  - The final backup file format.
- **Not touched:** the PROD package `MordWare`, the DEV package `MordWare.Dev`, the data of either, and the real `Documents\mordWare Backups` folders.

## Guardrails touched

- **1 (OSS only):** no new dependency. The probe app uses the Windows App SDK (MIT) already used by the app, and the S1 spike packages already pinned in `spikes/Directory.Packages.props`: `SQLite3MC.PCLRaw.bundle` and `Microsoft.EntityFrameworkCore.Sqlite.Core` (MIT), and `System.Security.Cryptography.ProtectedData` (MIT).
- **5 (data-safe):** the spike decides where the pre-migration backup and the daily backups can live so they outlast the package.
- **6 (no personal data in logs):** synthetic data only.
- **7 (DEV and PROD never mix):**
  - The spike runs under its own identity and its own test folders (`Documents\mordWare S3 spike`, `%LOCALAPPDATA%\mordWare S3 spike`), deleted at the end.
  - It never installs, launches or uninstalls PROD.
  - It proves the per-edition backup folders and the edition check in the backup header that Foundation will rely on.
- **9 (performance):** the live database stays on local, unsynced storage.

## User-visible

No. This is an internal spike, so there is no CHANGELOG entry.

## Roadmap

Item **[S3] Spike: MSIX storage and uninstall**. While applying, its status moves to `in progress` and then `done` with this change's name.

## Impact

- **New, isolated content:** `spikes/S3.Storage`. The app, `src/`, `tests/` and CI are unaffected.
- **Dependencies:** none new.
- **Changed docs:**
  - `docs/roadmap.md`: the [S3] and [1] Foundation notes
  - `docs/architecture.md`: the `spikes/` paragraph and the Editions section
- **On this PC:** a spike package is installed and removed several times while applying. Windows Developer Mode is required, as it already is for `scripts/run-dev.ps1`.
