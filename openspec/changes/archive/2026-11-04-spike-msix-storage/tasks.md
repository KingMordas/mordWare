# Tasks

## 1. Spike scaffolding

- [x] 1.1 In `docs/roadmap.md`, mark roadmap item [S3] `in progress` with change `spike-msix-storage`. Verify the table and the [S3] section both show it.
- [x] 1.2 Create the probe app `spikes/S3.Storage/S3.Storage.csproj` (design D1): packaged WinUI 3, Debug only, not added to `MordWare.slnx`.
  - Its manifest has identity `MordWare.Spike.S3`, display name "mordWare S3 spike", `runFullTrust`, and the app execution alias `s3probe.exe`.
  - It uses only packages already pinned in `spikes/Directory.Packages.props` or the root `Directory.Packages.props`.
  - Verify:
    - the project builds
    - `git diff --stat` shows both `Directory.Packages.props` files, `MordWare.slnx` and `src/` unchanged
    - `dotnet build MordWare.slnx` is unaffected
- [x] 1.3 Add the probe's command dispatch (design D2): with a command line, run one command, write `Documents\mordWare S3 spike\results\<command>.json`, then exit; with none, open the picker window. Also add the `where` command.
  - Verify: register the spike with `dotnet run -p:WinAppRunNoLaunch=true` and run `s3probe.exe where` from PowerShell. The result file appears, and its `LocalFolder` path contains `MordWare.Spike.S3`.
  - If the alias does not deliver the arguments, switch to the `Invoke-CommandInDesktopPackage` fallback and record which mechanism was used.
  - Result (2026-11-04): PASS, through the alias, so no fallback was needed. `Start-Process "$env:LOCALAPPDATA\Microsoft\WindowsApps\s3probe.exe" -ArgumentList where -Wait -PassThru` returns in about 350 ms with exit code 0 and no window.
    - `where.json` reports `LocalFolder` = `%USERPROFILE%\AppData\Local\Packages\MordWare.Spike.S3_<hash>\LocalState` and OS build 10.0.26300.
    - **Documents is redirected into OneDrive** (Known Folder Move): `%USERPROFILE%\OneDrive\<Documents>`, `documentsUnderOneDrive: true`. So check (f) can run.
    - Baseline before the spike: only the DEV edition (`MordWare.Dev`) is registered; PROD is not installed.
- [x] 1.4 Add the driver `spikes/S3.Storage/run-checks.ps1` (design D5). It needs install, uninstall and launch helpers that match the package name `MordWare.Spike.S3` exactly, plus a `-CleanupOnly` switch.
  - Verify a guard: running a helper with any other package name, such as `MordWare` or `MordWare.Dev`, throws before acting.
  - Verify `-CleanupOnly` removes the spike package and the spike folders.
- [x] 1.5 Update the `spikes/` paragraph of `docs/architecture.md` to name `S3.Storage`, with the note that it is a packaged probe app run on this PC only, not in CI. Verify the paragraph matches the files created above.

## 2. Where files really land (checks a, b, d)

- [x] 2.1 Add the `write` and `read` commands. Run check (b) through the driver, writing into both a new and a pre-existing folder under `%LOCALAPPDATA%`. Record per folder:
  - where the file landed (the real path, or under `Packages\MordWare.Spike.S3_<hash>\LocalCache\Local\`)
  - what the package reads back
  - what remains after uninstall
  - Verify the driver prints PASS / FAIL with the paths as evidence.
  - Result (2026-11-04, Windows build 26300, development-mode registration): PASS.
    - **New folder** `%LOCALAPPDATA%\mordWare S3 spike\probe.txt`:
      - The probe writes it and reads it back fine.
      - From outside, the real path does not exist. The file is at `Packages\MordWare.Spike.S3_<hash>\LocalCache\Local\mordWare S3 spike\probe.txt`.
      - Uninstall deletes it together with the whole package folder.
    - **Pre-existing folder** `%LOCALAPPDATA%\mordWare S3 spike existing\` (created outside the package first): the probe's new file lands at the **real** path, nothing goes under `LocalCache`, and the file **survives uninstall**.
    - **Conclusion for Foundation:** AppData is unpredictable for a packaged app. Whether a write survives uninstall depends on whether the folder happened to exist beforehand. Backups never go under AppData; Documents (or a user-picked folder) is the rule.
- [x] 2.2 Run check (a): a marker file written into `Documents\mordWare S3 spike\Backups\`, then uninstall. Record whether it survived and the resolved Documents path. Verify with the driver's PASS / FAIL step.
  - Result (2026-11-04): PASS. Documents resolves to `%USERPROFILE%\OneDrive\<Documents>` (Known Folder Move into OneDrive). The marker and a real encrypted backup (`spike-PROD-...s3bak`, written by `create-db` + `backup`) both survived uninstall, with no redirection.
- [x] 2.3 Run check (d): a marker in LocalState plus a file in the backup folder, then `Reset-AppxPackage`. Record that LocalState is wiped and the backup folder is intact. Verify with the driver's step.
  - Finding (2026-11-04, Dev mode): `Reset-AppxPackage` fails with "Removal failed". The AppXDeploymentServer log shows `0x80073CFA` wrapping `0x80070032` (ERROR_NOT_SUPPORTED): Windows does not reset a development-mode registration, and that includes DEV deployed by `run-dev.ps1`. With the owner's agreement, (d) runs on the Msix install mode (task 2.4, design D1).
  - Result (2026-11-04, Msix mode, signed install, `IsDevelopmentMode: False`): PASS.
    - `Reset-AppxPackage` wiped the LocalState marker and left the marker in the Documents backup folder intact. The package stayed installed.
    - Right after the cmdlet returns, `Get-AppxPackage` can briefly return nothing while Windows re-registers the package. The first run reported FAIL for that reason alone; the driver now waits up to 15 s.
    - **For Foundation:** Settings > Apps > Reset destroys the live database exactly like uninstall does. The backups and the recovery key are the only way back.
- [x] 2.4 Add the Msix install mode to the driver (design D1): copy the Debug layout to a `%TEMP%` staging folder, `makeappx pack`, `signtool sign` with the temporary self-signed certificate, then `Add-AppxPackage`. Run (d) on it, and repeat (a), (b), (c) and (i). Record any difference from Dev mode.
  - Verify:
    - the driver prints PASS / FAIL per check
    - `Get-AppxPackage MordWare.Spike.S3` shows `IsDevelopmentMode: False` during the run
    - cleanup removes the package, and both certificate copies (`CurrentUser\My` and `LocalMachine\TrustedPeople`) are gone (task 6.4)
  - Result (2026-11-04): PASS on a signed install (`IsDevelopmentMode: False`, `SignatureKind: Developer`). Same outcomes as Dev mode, with no difference:
    - **Unsigned did not work:** `Add-AppxPackage -AllowUnsigned` gives `0x80073D2B` ("an unsigned package cannot include Executable activations"). So the install is signed with a temporary self-signed `CN=mordWare` certificate (thumbprint `31895EEB...1171`, 3 days, non-exportable key), trusted by the owner in `LocalMachine\TrustedPeople`.
    - **(d):** see 2.3.
    - **(b):** a new `%LOCALAPPDATA%` folder is redirected under `LocalCache\Local` and deleted on uninstall; a pre-existing folder is written for real and survives.
    - **(a):** the marker and the backup in `OneDrive\<Documents>` survived uninstall.
    - **(i):** after uninstall and reinstall, restore with only the backup file and the recovery key gave 1000 rows; the next run opened from the re-created DPAPI key.
    - **(c):** the LocalState marker survived a same-version re-install and a **real package update, 0.1.0.0 -> 0.1.1.0**.
      - A first (c) run did not really update: `dotnet build` does not refresh the `AppX` layout (only the `dotnet run` deploy does), so the package stayed 0.1.0.0.
      - The driver now copies the source manifest's version into the staged manifest before packing. The re-run installed 0.1.1.0 over 0.1.0.0.

## 3. Updates and re-deploys (check c)

- [x] 3.1 Run check (c) with a marker in LocalState, under two scenarios:
  - (1) re-register at the same version through `dotnet run -p:WinAppRunNoLaunch=true`, the way `run-dev.ps1` deploys DEV
  - (2) bump the manifest version, rebuild and re-register
  - Record whether the marker survived each.
  - Verify with the driver step.
  - Also record whether a re-register ever removed LocalState. That would be a finding for `run-dev.ps1` and the DEV workflow.
  - Result (2026-11-04, Dev mode): PASS.
    - The marker survived a same-version re-register through `dotnet run` (the `run-dev.ps1` path) and an update to 0.1.1.0, both seen from outside and by the probe.
    - **No re-register removed LocalState,** so `run-dev.ps1` does not wipe DEV data on redeploy. Only `-Reset` (an explicit uninstall) does.
    - The Msix-mode run (a real package update) is recorded in task 2.4.

## 4. Backup, restore and editions (checks g, h, i)

- [x] 4.1 Add own copies of the S1 helpers (SQLCipher v4 connection, `KeyWrap`, `RecoveryKey`) and the `create-db` command. Verify: running `create-db` makes an encrypted `LocalState\Data\spike.db` that opens with the DPAPI-unwrapped key and refuses a wrong key (`SqliteException` 26).
  - Result (2026-11-04): PASS. 1000 rows, `journal_mode` wal, engine "SQLite3 Multiple Ciphers 2.6.1". A random wrong key gives `SqliteException 26: 'file is not a database'`. DPAPI CurrentUser works unchanged inside a full-trust package.
  - **Finding:** the recovery-wrapped data key must also be kept locally (`spike.recovery` next to `spike.key` in LocalState). Otherwise a scheduled backup could not write its header without asking for the recovery key. Both blobs live in LocalState and die with it; the recovery blob is copied into every backup.
- [x] 4.2 Add the `backup` command with the write sequence and draft header of design D3. Verify: the resulting `.s3bak` starts with `MWBK`, the payload hash matches, and no `.tmp` file remains.
  - Result (2026-11-04): PASS. Every `.s3bak` starts with `MWBK` and has the right edition byte (0 PROD, 1 DEV): 41,088 bytes = 128-byte header + 40,960-byte payload (10 pages). The payload hash is checked by every restore in 4.3-4.5. `tmpFilesLeft` is empty after each complete backup.
- [x] 4.3 Run check (g): `backup --interrupt` leaves only `.tmp` files, a restore picks the previous complete backup, and the next `backup` removes the leftovers. Verify with the driver step.
  - Result (2026-11-04, Dev mode): PASS.
    - The interrupted run (exit 3) left `...774.db.tmp` and `...774.s3bak.tmp`, and no `.s3bak`.
    - Restoring "newest in folder" picked the earlier complete `...443.s3bak` (1000 rows).
    - The next backup deleted both leftovers, leaving only complete `.s3bak` files.
- [x] 4.4 Add the `restore` command with the restore flow of design D3. Run check (h):
  - DEV and PROD backups land in their own folders
  - DEV -> PROD is refused
  - PROD -> DEV succeeds and re-wraps the key under DEV
  - a header with a flipped edition byte fails to unwrap
  - a truncated `.s3bak` is refused by its hash before anything is replaced
  - Verify with the driver step.
  - Result (2026-11-04, Dev mode): PASS. The first run reported FAIL only because the driver's assertion expected `*Cryptographic*` in the flipped-byte error; it was fixed to match `System.Security.Cryptography.*`. The evidence was identical in both runs:
    - The PROD backup went to `...\Backups`, the DEV one to `...\Backups (DEV)`.
    - DEV -> PROD: refused (`A DEV backup cannot be restored into PROD.`).
    - PROD -> DEV: restored, re-wrapped as DEV, and opens as DEV with 1000 rows.
    - Flipped edition byte: `AuthenticationTagMismatchException`, so header tampering is caught by the edition binding.
    - Truncated by 1000 bytes: `InvalidDataException` (payload hash mismatch), refused before anything was touched.
    - The live database file hash was unchanged by both refusals.
- [x] 4.5 Run check (i) end to end: `create-db`, `backup`, uninstall, reinstall, `restore` with only the backup file and the recovery key, then a fresh probe run opening the database from the re-created DPAPI key file. Verify the row count is equal and the driver prints PASS.
  - Result (2026-11-04, Dev mode): PASS.
    - After uninstall, LocalState was gone and the backup kept.
    - After reinstall, `open-db` failed (`spike.key` not found), as expected.
    - Restore with only the backup file and the recovery key: 1000 rows, `quick_check` ok.
    - The next probe run opened the database from the re-created DPAPI key file (1000 rows; a wrong key is still refused).
    - The Msix-mode run is recorded in task 2.4.

## 5. Folder picker and OneDrive (checks e, f; partly manual)

- [x] 5.1 Add the picker window: a "Pick folder" button stores the path in `LocalSettings`, and a "Write test file" button writes there. Run check (e) with the owner:
  - pick a folder outside Documents, write
  - close and relaunch the probe, write again
  - Verify the driver sees both files. Close the probe window afterwards.
  - Result (2026-11-04, Dev mode, done by the owner): PASS.
    - The owner picked `%USERPROFILE%\mordWare S3 spike picker`, outside Documents and AppData.
    - The Windows App SDK `Microsoft.Windows.Storage.Pickers.FolderPicker(AppWindow.Id)` works in the full-trust package.
    - The path persisted in `LocalSettings` across a restart, and both writes landed at the real path: `s3-picker-20261104-214540.txt` and `...-214601.txt`.
- [x] 5.2 Run check (f).
  - If `where` reports Documents under OneDrive: back up, wait for sync, the owner frees up space on the `.s3bak` in Explorer, then `restore` it. Verify the restore succeeds and record how long the download took.
  - Otherwise record (f) as UNVERIFIED with the reason.
  - Result (2026-11-04, Dev mode): PASS.
    - The backup was written to `OneDrive\<Documents>\mordWare S3 spike\f\` and synced.
    - The owner freed up space in Explorer. The first restore succeeded (1000 rows), but a driver bug (strict mode, a missing `error` property on success) lost the "cloud-only before" evidence and the timing.
    - After fixing the driver, the file was made cloud-only again with `attrib -P +U` (attributes 0x501620: Offline, Unpinned, RecallOnDataAccess) and restored again: **ok, 1000 rows, 609 ms including the on-demand download** (about 40 KB). Afterwards the file was hydrated (0x420).
    - A full-trust packaged app reading a cloud-only file triggers the download transparently.

## 6. Outcome, docs and cleanup

- [x] 6.1 Rewrite the [S3] section of `docs/roadmap.md` with the outcome (PASS / FAIL / UNVERIFIED per check, with evidence, and the Windows build) and mark [S3] `done` with this change. Verify the old parked assumptions are replaced by decided or measured statements.
- [x] 6.2 Update the [1] Foundation notes in `docs/roadmap.md` with what it inherits:
  - the folder layout of design D4
  - the backup write sequence, draft header and restore flow of design D3
  - no DPAPI copy in backups, so every restore needs the recovery key
  - backups on the first launch of the day, before every migration and on demand, never on exit
  - `IAppPaths` gains a backup folder
  - deleting `spikes/S3.Storage`
  - Close the [S1] open question on where the key blobs live.
  - Verify the question no longer appears as open and each item above is present.
- [x] 6.3 Update the Editions section of `docs/architecture.md` with the measured storage facts: what uninstall, reset and update do, and where AppData writes really go. Verify the "Backups must therefore live outside the package" line now names where they live.
- [x] 6.4 Run `run-checks.ps1 -CleanupOnly`. Verify:
  - `Get-AppxPackage MordWare.Spike.S3` returns nothing, in either install mode
  - no `mordWare S3 spike*` folder remains under Documents, `%LOCALAPPDATA%` or `%TEMP%` (the Msix staging folder)
  - nothing named `MordWare.Spike.S3*` remains under `%LOCALAPPDATA%\Packages`
  - the `MordWare` and `MordWare.Dev` registrations are as they were before the spike
  - the temporary spike certificate is gone from `Cert:\CurrentUser\My` and `Cert:\LocalMachine\TrustedPeople`, and its `.cer` file is deleted
  - Result (2026-11-04): PASS.
    - Cleanup removed the spike package and deleted these folders:
      - `OneDrive\<Documents>\mordWare S3 spike`
      - `%LOCALAPPDATA%\mordWare S3 spike existing` (the new-folder trap had already gone with the package)
      - `%TEMP%\mordWare S3 spike msix`
      - `%USERPROFILE%\mordWare S3 spike picker`
    - No `MordWare.Spike.S3*` package folder is left.
    - The registrations equal the baseline: only the DEV edition (`MordWare.Dev`).
    - Certificate `31895EEB...1171`: the private-key copy was deleted from `CurrentUser\My` by Claude, and the `LocalMachine\TrustedPeople` entry was removed by the owner (elevated). It is absent from both, and from both Root stores. The `.cer` file is deleted.
- [x] 6.5 Run `dotnet build MordWare.slnx`, the architecture tests (`dotnet test --project tests/MordWare.Tests.Architecture`) and `openspec validate spike-msix-storage --strict`. Verify all three succeed.
  - Result (2026-11-04): PASS. `MordWare.slnx` builds with 0 warnings and 0 errors, the architecture tests pass 5/5, and the change is valid. `git status` shows no `bin/` or `obj/` content of the spike.

## Workflow follow-up

- Run /opsx:archive. There are no delta specs (`skip_specs`), so archive moves the change, confirms [S3] is `done` in `docs/roadmap.md`, and ends with one Conventional Commit on `main`.
- No CHANGELOG entry: the change is not user-visible.
