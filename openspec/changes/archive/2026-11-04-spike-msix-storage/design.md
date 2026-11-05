# Design

## Context

- **The app today:**
  - `MordWare.App` is a packaged WinUI 3 app with the `runFullTrust` capability, on Windows App SDK 2.6.5.
  - `PackagedAppPaths` puts `Data\` and `Logs\` under `ApplicationData.Current.LocalFolder` (LocalState). `IAppPaths` has no backup folder yet.
  - Debug builds package the `MordWare.Dev` identity. `scripts/run-dev.ps1` deploys it with `dotnet run -p:WinAppRunNoLaunch=true`, which registers the package from the build output, and then starts it through its AUMID.
- **Known facts** (`docs/architecture.md`, Editions): uninstalling a package deletes its LocalState, and DEV and PROD side by side keep separate LocalStates.
- **Unknown on this PC:**
  - how file-system write virtualization treats a full-trust package's writes under `%LOCALAPPDATA%` / `%APPDATA%`
  - whether Documents is redirected into OneDrive (Known Folder Move)
- **What [S1] left to reuse** (`spikes/S1.Encryption`):
  - the SQLCipher v4 connection (URI plus raw key)
  - `KeyWrap`: DPAPI CurrentUser plus HKDF / AES-256-GCM under the recovery key, both bound to the edition, with a 77-byte recovery blob
  - `RecoveryKey`: 30 Crockford base32 symbols
  - the SQLite backup API to a keyed destination

See proposal.md for why the spike exists and the decisions it validates.

## Goals / Non-Goals

**Goals:**
- Each check (a) to (i) ends in PASS, FAIL or UNVERIFIED with evidence (paths seen from inside and outside the package, file listings, exceptions). A FAIL is a valid spike result and gets recorded.
- Foundation can adopt the folder layout, the backup write sequence, the header draft and the restore flow without making a new decision.
- Everything the spike creates on this PC is gone at the end: the spike package and both spike test folders.

**Non-Goals:**
- Production-quality UI, localization or error handling in the probe app. It shows one window with two buttons, in English only.
- Backup scheduling, retention or rotation.
- The final backup format, the file naming scheme and the recovery-key check symbol.
- A CI-runnable test. Installing and removing packages needs Developer Mode and an interactive session, so the spike runs on this PC only.

## Decisions

### D1. A packaged probe app under its own identity, driven from outside

```
spikes/
  Directory.Packages.props        unchanged: reuses SQLite3MC, EF Core Sqlite.Core, ProtectedData
  S1.Encryption/  S2.Decimals/    untouched
  S3.Storage/
    S3.Storage.csproj             WinUI 3, packaged, Debug only, NOT in MordWare.slnx
    Package.appxmanifest          Identity MordWare.Spike.S3, runFullTrust, app execution alias s3probe.exe
    App.xaml(.cs)                 with a command line: run one probe command headless, then exit
                                  without one: the picker window (check e)
    Probe/                        commands (D2), own copies of S1 SpikeDb / KeyWrap / RecoveryKey
    run-checks.ps1                driver (D5): install, run, update, reset, uninstall, inspect from outside
```

- **Why packaged and not a test project.** Virtualization, LocalState and uninstall only exist for a process with package identity. An xUnit test host has none, so it would prove nothing. The probe runs inside the package, and the driver checks from outside it, as a plain PowerShell process, what really landed on disk.
- **Its own identity, `MordWare.Spike.S3`.** It shares nothing with `MordWare` or `MordWare.Dev`, so installing, resetting and uninstalling it can never touch either edition. The driver matches the package name exactly, as `run-dev.ps1` does, and refuses to act on any other.
- **Launched with arguments through an app execution alias** (`uap5:AppExecutionAlias`, `s3probe.exe <command> ...`).
  - An aliased launch keeps the package identity and passes the command line.
  - The probe writes its result as JSON to `Documents\mordWare S3 spike\results\<command>.json`, because a WinUI process has no console to answer on. The driver waits for that file.
  - **Fallback** if the alias does not deliver the arguments: `Invoke-CommandInDesktopPackage` (built into Windows) starts the probe inside the package's context. Record which mechanism was used.
- **Two install modes** (added during apply, 2026-11-04):
  - **Dev:** a development-mode registration by `dotnet run`, exactly as `run-dev.ps1` deploys DEV.
  - **Msix:** a real `.msix` installed with `Add-AppxPackage`, the way an installed PROD behaves.
    - It is built from the same Debug layout with `makeappx` and signed with `signtool`, both from the Windows SDK BuildTools package the app already uses.
    - **Unsigned was tried first and refused:** `-AllowUnsigned` fails with `0x80073D2B`, because an unsigned package cannot include executable activations.
    - **The signature comes from a temporary self-signed certificate** (owner-approved 2026-11-04):
      - subject `CN=mordWare`, code signing only, private key non-exportable in `CurrentUser\My`, valid for 3 days
      - its public part is trusted by the owner in `LocalMachine\TrustedPeople` with one elevated command, and removed with another at the end
      - free, so guardrail 1 is unaffected
      - It is a dry run of the sideload signing that the roadmap's release item ([L]) will need.
  - **Why:** Windows refuses `Reset-AppxPackage` on a development-mode registration (`0x80070032`, not supported). So check (d) needs the Msix mode, and (a), (b) and (i) are repeated on it, so the PROD-like install path is covered as well as the DEV one.
  - Only one mode is installed at a time. Cleanup removes every registration named `MordWare.Spike.S3`.
- **Own copies of the S1 helpers,** as S2 did. Foundation deletes each spike on its own, so no spike may reference another.
- **Simulated editions.** The spike identity is neither DEV nor PROD. Commands take `--edition DEV|PROD`, which selects:
  - the edition written into the backup header
  - the edition binding of the key wraps
  - the default backup folder name (D4)
- **Alternatives rejected:**
  - Running the checks inside the real DEV package: it would put DEV's data at risk and need a DEV-only code path in the app.
  - A packaged console app: WinUI is needed for the folder picker in (e). One project serving both modes is simpler than two packages.

### D2. Probe commands

| Command | Does | Serves |
|---|---|---|
| `where` | Reports `LocalFolder.Path`, the resolved Documents folder, and whether it lies under a OneDrive root (`%OneDrive%`, `%OneDriveConsumer%`, `%OneDriveCommercial%`) | a, f |
| `write <path> <marker>` | Writes a marker file to the given path, creating its folders | a, b, c, d |
| `read <path>` | Reports whether the package sees the file, and its content | b, c |
| `create-db --edition E` | Creates an encrypted database `LocalState\Data\spike.db` (WAL) with N synthetic rows, the DPAPI-wrapped data key in `LocalState\Data\spike.key`, and a recovery key. The recovery key is synthetic and spike-only, written to the results file for the driver | i |
| `backup <folder> --edition E [--interrupt]` | Runs the backup write sequence of D3. `--interrupt` exits the process after the `.tmp` is written and before the rename | a, g, h, i |
| `restore <file> <recovery key> --edition E` | Runs the restore flow of D3 and reports the row count, or the refusal | h, i |

### D3. Backup write sequence, draft header and restore flow

**Write sequence**, all inside the destination folder, so the final rename never crosses volumes:

```
 live spike.db (WAL, LocalState)
      |  SQLite backup API, destination keyed with the same data key (S1)
      v
 <name>.db.tmp
      |  write header + db bytes in one go to a new file, flush to disk (write-through)
      v
 <name>.s3bak.tmp  --rename-->  <name>.s3bak        then delete <name>.db.tmp
```

- **An interruption at any point leaves only `.tmp` files,** which restore ignores and the next backup run deletes. A file named `.s3bak` is therefore always complete.
- **The header carries a SHA-256 of the payload,** so a truncated or corrupted file is detected before anything is overwritten.

**Draft header** (only for this spike; Foundation finalizes the format):

| Field | Size | Note |
|---|---|---|
| Magic `MWBK` | 4 | |
| Format version | 2 | 1 |
| Edition | 1 | DEV / PROD |
| Schema version | 4 | 0 in the spike |
| Created (UTC ticks) | 8 | |
| Recovery blob | 77 | the S1 format, bound to the edition |
| Payload SHA-256 | 32 | over the db bytes |
| Payload | rest | the encrypted SQLite file, unchanged |

- **No DPAPI copy in the header** (proposal decision). Restore always needs the recovery key.
- **The header is plaintext, and that is safe:** tampering with its edition byte makes the recovery unwrap fail, because the wrap is bound to the edition (S1).
- **Alternatives rejected:**
  - A header inside the database: the database is encrypted, but the header must be readable before the key is known.
  - A sidecar file next to the database: two files can be separated or half-synced by OneDrive.
  - A zip container: it adds a dependency or compression for no gain, since the payload is ciphertext and does not compress.

**Restore flow:**

```
 read header -> magic / version ok? -> payload hash ok?
      -> edition rule: same edition ok | PROD backup -> DEV ok | DEV backup -> PROD refused
      -> unwrap the data key with the recovery key under the header's edition
      -> write the payload to LocalState\Data\spike.db.restore.tmp, open it with the key, check rows
      -> rename over spike.db, then re-wrap the data key with DPAPI under the running edition
```

- The live database is replaced only after the restored copy has opened and checked out.
- A PROD -> DEV restore re-wraps under DEV, as the Foundation notes require.

### D4. Folder layout under test

| Purpose | Spike location (deleted at the end) | Foundation equivalent |
|---|---|---|
| Live database + DPAPI key file | `LocalState\Data\` | same, per edition |
| Backups, PROD | `Documents\mordWare S3 spike\Backups\` | `Documents\mordWare Backups\` |
| Backups, DEV | `Documents\mordWare S3 spike\Backups (DEV)\` | `Documents\mordWare Backups (DEV)\` |
| AppData trap, new folder (b) | `%LOCALAPPDATA%\mordWare S3 spike\` (not created beforehand) | never used |
| AppData trap, existing folder (b) | `%LOCALAPPDATA%\mordWare S3 spike existing\` (created beforehand by the driver) | never used |
| Probe results | `Documents\mordWare S3 spike\results\` | none |

- **Documents is resolved the way Foundation would resolve it** (`Environment.SpecialFolder.MyDocuments`), so a Known Folder Move into OneDrive is seen exactly as the app will see it.
- **The remembered folder** (check e) is stored in `ApplicationData.Current.LocalSettings`. It is lost on uninstall or reset, which is acceptable: a reinstall starts with "restore from a backup file" anyway.
- **(b) tests both a new and an existing folder,** because write virtualization treats files that already exist outside the package differently from new ones.

### D5. Driver script and manual steps

`run-checks.ps1` runs each check as a step that prints PASS, FAIL or UNVERIFIED with evidence. It never needs elevation.

| Check | How |
|---|---|
| a | Install. `write` + `backup` into `Backups\`. `Remove-AppxPackage`. Check the files from outside the package. |
| b | `write` into both AppData folders. From outside, look for each file at the real path and under `Packages\<family>\LocalCache\Local\`. `read` from inside. Uninstall, look again. |
| c | Write a marker into LocalState. Bump the manifest version, rebuild and re-register; also re-run `dotnet run -p:WinAppRunNoLaunch=true` at the same version. `read` the marker after each. |
| d | Msix mode. Marker in LocalState + a backup. `Reset-AppxPackage`. Marker gone, backup intact. (Dev mode records the refusal.) |
| e | **Manual:** the owner launches the probe without arguments, picks a folder, writes a test file, closes the probe, relaunches it and writes again. The driver checks both files. |
| f | `where` says whether Documents is under OneDrive. **If yes:** back up, wait for sync, the owner chooses "Free up space" on the backup in Explorer, then `restore` from it. **If no:** UNVERIFIED, with the reason recorded. |
| g | `backup --interrupt`: only `.tmp` files remain, and `restore` of the folder's newest `.s3bak` still picks the previous complete backup. The next `backup` removes the leftovers. |
| h | `backup --edition DEV` and `--edition PROD` land in their own folders. Restore DEV -> PROD is refused, PROD -> DEV succeeds. A header with a flipped edition byte fails to unwrap. |
| i | `create-db`, `backup`, uninstall, reinstall, `restore` with the recovery key alone, row count equal, the DPAPI key file re-created and the database opening from it on the next probe run. |
| a, b, i (Msix) | The same steps as above, on the Msix install. |
| cleanup | Uninstall every spike package (both modes), delete the Msix staging folder under `%TEMP%`, delete both `mordWare S3 spike` folders under Documents and `%LOCALAPPDATA%`, and check that nothing named `MordWare.Spike.S3` remains under `%LOCALAPPDATA%\Packages`. |

## Risks / Trade-offs

- [The alias does not pass the arguments to a WinUI process, or the launch is not awaitable] -> Use the `Invoke-CommandInDesktopPackage` fallback (D1). The result file and a timeout make every step awaitable either way.
- [Documents is not under OneDrive on this PC, so (f) cannot run] -> Record (f) as UNVERIFIED with the reason. The atomic write (g) is the part that protects a synced folder, and it is proven regardless. Foundation's first-run screen still says whether the chosen folder is synced.
- [The spike's synthetic files sync to OneDrive and stay in its recycle bin after cleanup] -> Synthetic data only, no personal data (guardrail 6). Note it in the results.
- [A failed run leaves the spike package or test folders behind] -> The driver's cleanup step also runs on its own (`run-checks.ps1 -CleanupOnly`), and the final task checks that nothing remains.
- [Results depend on this Windows build (26300)] -> Record the build in the task notes. Foundation relies only on behavior that is documented for MSIX as well as observed here.
- [The spike prints a synthetic recovery key to a results file] -> It protects only synthetic spike data, and the results folder is deleted at the end.

## Migration Plan

None. The spike creates no app schema and ships nothing. To roll it back, uninstall the spike package and delete `spikes/S3.Storage` and the spike test folders; the cleanup step does both.

## Open Questions

- The backup file name pattern (for example `mordWare-PROD-2026-11-04T0930.mwbak`) and the final extension. Foundation decides them with the retention logic; the spike uses `.s3bak`.
