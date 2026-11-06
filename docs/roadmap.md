# Roadmap

The single, ordered plan for mordWare, and the source of truth for **"what's next?"**:

1. Run `openspec list --json`. An active change with open tasks needs **apply**; one with all tasks done needs **archive**.
2. Otherwise, the first item below that is not `done` is next. With status `todo` it needs **explore** (or **propose** if its decisions are already made); with status `explored` it needs **propose**.

Every change that completes or reshapes an item updates this file in the same change. Ideas, decisions and loose ends found along the way go into the item's **Parked notes**, never only into a conversation. Like everything in this repository, this file is public: no personal, machine-specific or earlier-web-app details (guardrail 11, [`public-safety.md`](public-safety.md)).

Status values: `todo`, `explored` (decided, ready to propose), `in progress` (a change exists), `done`.

| # | Item | Status | Change |
|---|---|---|---|
| 0 | Bootstrap | done | `2026-11-04-bootstrap-solution` (archived) |
| S1 | Spike: encryption library | done | `2026-11-04-spike-encryption` (archived) |
| S2 | Spike: decimal storage on SQLite | done | `2026-11-04-spike-decimal-storage` (archived) |
| S3 | Spike: MSIX storage and uninstall | done | `2026-11-04-spike-msix-storage` (archived) |
| R | Open source and public repository | done | `2026-11-05-replan-open-source-delivery` (archived) |
| S4 | Spike: unpackaged app, installer, updates from GitHub | done | `2026-11-05-spike-unpackaged-installer` (archived) |
| D | Delivery (unpackaged app, editions, release workflow) | done | `2026-11-06-delivery-unpackaged` (archived) |
| 1 | Foundation (persistence + shell + Home dashboard frame) | todo | - |
| U | Update check at startup | todo | - |
| 2 | morVehicles | todo | - |
| 3 | Importer (private tool, outside this repository) | todo | - |
| 4 | morHouses | todo | - |
| 5 | morFinance | todo | - |
| v1.0.0 | First release | todo | - |
| L | Later / unscheduled | - | - |

---

## Direction (decided 2026-11-05)

- **What mordWare is.** A free, open-source (GPL-3.0-only) personal household assistant for Windows 11: vehicles, houses and finance, with the data only on the user's PC. It is a hobby project developed in spare time and provided as-is.
- **License and contributions (decided 2026-11-05, change `switch-license-gpl3`).** GPL-3.0-only, open source but closed to contribution: pull requests are limited to the owner and Issues are off. Anyone may redistribute mordWare only under GPL-3.0 with its full source; the owner, as sole copyright holder, may also sell it or ship a Microsoft Store edition under other terms later.
- **Three modules:** Vehicles, Houses and Finance, with the same features as their earlier web versions but a **new UI and UX**: the old screens are input, not a template. A main **Home dashboard** gives the app its "household assistant" context. Pantry is retired and will not be ported.
- **One Windows account = one data set.** No profiles, logins or roles. "Family" or "household" is the app's tone, not a multi-user feature.
- **Delivery.** No code signing, and no Microsoft Store for now (a Store edition, which Microsoft signs, stays possible later). An unsigned MSIX cannot be installed ([S3]), so mordWare becomes an **unpackaged** app with a per-user installer published on **GitHub Releases** and linked from the project website. Windows SmartScreen will warn on first run; that is accepted.
- **Releases.** A pushed `v*.*.*` tag on `main` builds, tests and publishes a release. There are **no releases before v1.0.0**, which ships once all three modules are complete and working, and no `v*.*.*` tags before it. The one recorded exception is [S4]'s temporary `s4-feed-*` pre-releases: never `v*.*.*`, on a Velopack channel of their own, published and deleted only with the owner's approval at each step, and deleted right after the check.
- **Updates.** The app checks GitHub Releases at startup. The check is on by default, can be turned off, sends no user data, and installs only after the user clicks "Update".

## [0] Bootstrap

- **Status:** done (2026-11-04). Change `bootstrap-solution`, archived as `openspec/changes/archive/2026-11-04-bootstrap-solution/`. Specs: `app-environments`, `development-workflow`.
- **Scope:**
  - .NET 10 solution with Core / Persistence / Integrations / Vehicles / App and the test projects.
  - Architecture tests.
  - DEV (`MordWare.Dev`) and PROD (`MordWare`) packaged editions, plus `IAppPaths`.
  - Localized empty window.
  - `CLAUDE.md`, `openspec/config.yaml`, this roadmap, CI.
- **Parked notes:**
  - Uninstalling an MSIX package deletes its LocalState (verified while applying bootstrap). This fed S3.
  - The DEV marker reads "SVIL" in Italian. Revisit if "DEV" should stay untranslated.
  - The logos are placeholders (teal "mW", orange DEV band).
  - `scripts/run-dev.ps1` was added during apply: the owner's way to build and launch DEV. [D] reworks it for the unpackaged app.
  - CI exists but has never run on GitHub; it activates with the public repository ([R]).

## [S1] Spike: encryption library

- **Status:** done (2026-11-04). Change `spike-encryption`, archived as `openspec/changes/archive/2026-11-04-spike-encryption/` (no specs: a spike changes no app behavior). Proof: `spikes/S1.Encryption` (xUnit: 37 tests plus 1 opt-in manual check, outside the solution); results per criterion in the change's task notes.
- **Question:** which free, OSS-licensed SQLite encryption works with EF Core on .NET 10?
- **Outcome:**
  - **Library: SQLite3 Multiple Ciphers.** `SQLite3MC.PCLRaw.bundle` 2.6.1 (MIT) with `Microsoft.EntityFrameworkCore.Sqlite.Core` 10.0.14, which never brings a second bundle.
    - It resolves `SQLitePCLRaw.core` 3.0.2 (Apache-2.0) underneath Microsoft.Data.Sqlite 10. EF Core, including migrations, works on it.
    - Native `sqlite3mc.dll` for win-x64, x86 and arm64; SQLite 3.54.0.
    - The free SQLCipher route is gone: SQLitePCLRaw 3.0 deprecated `bundle_e_sqlcipher` and removed `bundle_e_sqlite3mc`, and Zetetic's packages are commercial.
  - **Cipher: SQLCipher v4 scheme.** Selected per connection with the URI `file:<percent-encoded path>?cipher=sqlcipher&legacy=4`.
    - The files are standard SQLCipher 4: they opened in DB Browser for SQLCipher (SQLCipher 4.6.1) with a raw key and with a passphrase.
    - The library's default (ChaCha20) was not chosen; SQLite3MC 2.6.x has a known ChaCha20 issue.
  - **Data key: a random 256-bit raw key**, passed as `Password=x'<64 hex>'`.
    - It skips SQLCipher's PBKDF2: open + first query takes 0.3 ms, against 90 ms with a passphrase.
  - **Refusal.** A wrong key or no key throws `SqliteException` 26 ("file is not a database"); a wrong key throws from `Open()` itself. The file stays intact.
  - **At rest.** With WAL, no plaintext appears in the `.db`, `-wal` or `-shm` files.
  - **Key chain** (base-library and Microsoft crypto only):
    - DPAPI (CurrentUser) wraps the data key for daily use.
    - HKDF-SHA256 + AES-256-GCM under the recovery key wraps it for restore. The recovery blob is 77 bytes (version 1 | salt 16 | nonce 12 | ciphertext 32 | tag 16).
    - Both wraps are bound to the edition: a PROD blob never unwraps as DEV.
  - **Argon2id dropped.** The recovery key is generated with 150 bits, so a password-stretching KDF adds nothing. A user-chosen passphrase would need this revisited.
  - **Recovery key format.** 30 Crockford base32 symbols (no I, L, O, U), shown as six groups of five. Parsing ignores case, spaces and hyphens, and reads I/L as 1 and O as 0.
  - **Backup.** SQLite's backup API works from a live WAL database, either to a destination with the same key or to one with its own key (the copy is re-encrypted).
    - SQLite3MC refuses to back up into an unkeyed destination, so a backup can never be plaintext by accident.
  - **Speed.** 10,000 rows insert and read about 1.1x slower than unencrypted.

## [S2] Spike: decimal storage on SQLite

- **Status:** done (2026-11-04). Change `spike-decimal-storage`, archived as `openspec/changes/archive/2026-11-04-spike-decimal-storage/` (no specs: a spike changes no app behavior). Proof: `spikes/S2.Decimals` (xUnit: 79 tests, outside the solution, on the encrypted S1 connection); results per criterion in the change's task notes.
- **Question:** how to store money, liters and odometer readings so SQL filtering, ordering and uniqueness are correct.
- **Outcome:**
  - **Decimal as TEXT is rejected** (evidence on EF Core 10.0.14).
    - It gives correct LINQ results, but only by rewriting SQL around the column: `ORDER BY ... COLLATE EF_DECIMAL` and `WHERE ef_compare(col, ...) > 0`. Both helpers exist only on EF's own connection.
    - Range filters therefore never use an index (`SCAN`).
    - Plain SQL sorts as text (`-2.0, 10.0, 100.5, 9.0`).
    - Uniqueness holds only because EF normalizes the text: a raw `'100.50'` slips in next to `100.5`.
  - **Storage:** every decimal quantity is a signed 64-bit INTEGER at a fixed scale. Storage never rounds: a value with too many decimals, or out of range, throws.

    | Quantity | Scale |
    |---|---|
    | Money, including prices per unit | x10^4 |
    | Odometer, km intervals | x100 |
    | Liters | x1000 |
    | kWh, Smc, meter readings | x1000 |
    | Rate percent | x10^4 |
    | FX rate | x10^8 |

  - **Value types.** One `readonly record struct` per quantity (`Km`, `Liters`, `Money`, ...) over the scaled `long`.
    - Each implements `IScaled<TSelf>` (`static abstract Decimals`, `FromScaled`), so one generic `ScaledConverter<T>` takes the scale from the type and no mapping carries a hand-written scale.
    - An expression tree cannot call a static abstract member (CS8927), so the converter calls it through an ordinary static helper.
  - **Query shape: direct mapping.**
    - Entities use the domain types themselves, so there is no data-layer copy and no mapping code.
    - `r.OdometerKm > from` (any comparison operator, or `CompareTo`), `OrderBy`, `==`, `Min` and `Max` translate to the bare INTEGER column with the index used: `SEARCH ... USING COVERING INDEX (VehicleId=? AND OdometerKm>?)`, with no sort step.
    - Never write `.Scaled`, `EF.Property<long>` or a cast in a query: the first two throw, and the cast wraps the column and loses the index.
  - **Money: two scalar properties.**
    - `XxxAmount` (`MoneyAmount`, x10^4, any sign) and `XxxCurrency` (TEXT(3)), plus an unmapped `Money` view of the pair.
    - Indexed `(Currency, Amount)` ranges and ordering within a currency use the index.
    - EF Core 10 complex properties were rejected: they round-trip, but cannot declare an index on their members, and `Money`'s operators do not translate on them.
    - `Money` comparison and `+` throw on mixed currencies. Display rounds to the currency's exponent with an explicit `MidpointRounding`.
  - **Uniqueness is numeric.** `12345.1` and `12345.10` are the same integer, so a unique index refuses the second from EF (`DbUpdateException` over `SqliteException` 19 / extended 2067) and from raw SQL alike.
  - **Stats in memory.** 10,000 refuels load (projection, `AsNoTracking`) and compute (totals, l/100 km between full tanks, average/min/max price per liter) in a median of 13.4 ms (Release), exactly equal to a plain-`decimal` reference.
  - **Range.** Money at x10^4 holds +/-922 trillion. A value outside a type's range is refused, never truncated.

## [S3] Spike: MSIX storage and uninstall

- **Status:** done (2026-11-04). Change `spike-msix-storage`, archived as `openspec/changes/archive/2026-11-04-spike-msix-storage/` (no specs: a spike changes no app behavior). Proof: `spikes/S3.Storage`, a packaged probe app (`MordWare.Spike.S3`) plus its driver `run-checks.ps1`, outside the solution; results per check in the change's task notes.
- **Superseded in part (2026-11-05).** mordWare will not ship as an MSIX package (see Direction). The findings marked **(package-only)** below hold only for packaged apps and no longer drive the design; [S4] and [D] replace them. The rest (backups in a user-chosen folder, OneDrive behavior, atomic writes, edition binding, restore with the recovery key) still applies.
- **Conditions:** Windows build 26300. Checked in two install modes:
  - **Dev:** a development-mode registration by `dotnet run`, as `run-dev.ps1` deploys DEV.
  - **Msix:** a signed install like PROD's, using a temporary self-signed certificate that has since been removed.
  - Documents was redirected into OneDrive (Known Folder Move) on the test PC.
- **Question:** where do the live database and the backups live, given that uninstall deletes the package's LocalState?
- **Decisions** (explore 2026-11-04, validated by the checks below):
  - **Live database:** package LocalState, per edition. Never in a synced folder. **(package-only; [S4] decided the unpackaged location: `%LOCALAPPDATA%\mordWare Data`)**
  - **Backups:** a folder the user chooses, outside the app's install folder and outside AppData.
    - The default is `Documents\mordWare Backups` (PROD) or `Documents\mordWare Backups (DEV)`, confirmed by the user on first run, never chosen silently.
    - If Documents is synced by OneDrive, so are the backups; that is fine, because they are encrypted and written whole.
  - **Every restore needs the recovery key.** A backup never carries a DPAPI-wrapped key.
  - **Backups run** on the first launch of the day, before every migration, and on demand ("Back up now"). Never on exit.
- **Outcome** (all PASS):

  | Check | Result |
  |---|---|
  | (a) Backup in Documents vs. uninstall | Survives, in both modes. |
  | (b) Writes under `%LOCALAPPDATA%` | **(package-only)** A packaged app's write to a *new* folder is redirected into `Packages\<family>\LocalCache\Local\` and deleted on uninstall; a write to a folder that *already existed* goes to the real path and survives. |
  | (c) LocalState vs. re-deploy and update | **(package-only)** Survives a same-version `dotnet run` re-register and a real package update (0.1.0.0 -> 0.1.1.0). |
  | (d) Settings > Apps > Reset | **(package-only)** Wipes LocalState, exactly like uninstall, and leaves the backup folder intact. Windows refuses Reset on a development-mode registration (`0x80070032`). |
  | (e) Folder picker | The Windows App SDK `FolderPicker(AppWindow.Id)` works in the full-trust package; the path is remembered and stays writable after a restart. [S4] rechecks it unpackaged. |
  | (f) OneDrive, cloud-only file | Restore transparently downloads a "Free up space" backup: 609 ms for about 40 KB, 1000 rows intact. |
  | (g) Interrupted backup | Leaves only `.tmp` files. "Newest backup" picks the last complete one, and the next run deletes the leftovers. |
  | (h) Editions and tampering | DEV and PROD backups go to separate folders. DEV -> PROD is refused; PROD -> DEV succeeds and re-wraps under DEV. A flipped edition byte fails the unwrap (AES-GCM tag), and a truncated file fails its hash. The live database is untouched by every refusal. |
  | (i) Reinstall and restore | After uninstall and reinstall, the backup file and the recovery key alone restore everything (1000 rows, `quick_check` ok), and the DPAPI key file is re-created. |

  - **Key storage (closes the [S1] open question):**
    - The data folder holds the DPAPI-wrapped data key and the recovery blob (data key under the recovery key). Both live and die with the database.
    - The recovery blob must be kept locally too: without it, a scheduled backup could not write its header without asking for the recovery key.
    - Every backup header carries a copy of the recovery blob.
  - **Install and signing facts (package-only):**
    - An unsigned package with an executable cannot be installed (`0x80073D2B`). This is why mordWare goes unpackaged.
    - `dotnet build` does not refresh the `AppX` layout; only the `dotnet run` deploy does.

## [R] Open source and public repository

- **Status:** done (2026-11-05). Change `replan-open-source-delivery`, archived as `openspec/changes/archive/2026-11-05-replan-open-source-delivery/`. Spec: `public-repository`. The go-public steps (squash, push, public visibility) follow the archive commit, each confirmed by the owner.
- **Scope:**
  - Re-plan this roadmap and the living docs for the new direction.
  - `LICENSE` and the as-is / hobby notice. The license was first MIT and was replaced by GPL-3.0-only before anything was published (change `switch-license-gpl3`, archived as `openspec/changes/archive/2026-11-05-switch-license-gpl3/`).
  - The public-safety rule (guardrail 11) and its enforcement: [`public-safety.md`](public-safety.md), `scripts/check-public-safety.ps1`, git hooks in `.githooks/`, a gitleaks scan in CI, a Claude Code hook.
  - Remove personal, machine-specific and earlier-web-app details from all tracked content, archived changes included.
  - After archive: the public author identity, a squash into one clean initial commit, push to `KingMordas/mordWare`, and public visibility, each step confirmed by the owner.
- **Parked notes:**
  - CI becomes active with the push. Standard GitHub-hosted runners are free for public repositories, but every job keeps a timeout.
  - **The first CI run failed** and exposed three latent defects, fixed by change `fix-ci-first-run` (archived as `openspec/changes/archive/2026-11-05-fix-ci-first-run/`) before going public:
    - restore did not use the Release configuration (ReadyToRun runtime pack missing);
    - the safety script crashed when no private deny list exists (as in CI);
    - the architecture tests never found or loaded a Release build (platform folder, x86 ReadyToRun output).

## [S4] Spike: unpackaged app, installer, updates from GitHub

- **Status:** done (2026-11-05). Change `spike-unpackaged-installer`, archived as `openspec/changes/archive/2026-11-05-spike-unpackaged-installer/` (no specs: a spike changes no app behavior). Proof: `spikes/S4.Installer` (deleted by [D] once adopted), an unpackaged WinUI 3 probe app with Velopack (packIds `MordWare.Spike.S4` and `MordWare.Spike.S4.Dev`), its driver `run-checks.ps1` and its binary scanner `scan-package.ps1`, outside the solution; results per check in the change's task notes.
- **Conditions:** Windows build 26300, x64; .NET SDK 10.0.403, Windows App SDK 2.6.5, Velopack and `vpk` 1.2.161 (MIT). Per user, no admin rights, and no Windows setting changed (Smart App Control was off on the test PC and stayed untouched; Windows Sandbox is not enabled and was not used).
- **Question:** how does an unpackaged WinUI 3 mordWare install, upgrade, update itself from GitHub Releases and uninstall, without signing, and where does each edition's data live?
- **Decisions** (explore 2026-11-05, validated by the checks below):
  - **Installer and updater: Velopack.** Inno Setup and WiX are not needed.
  - **DEV runs from the build output** for daily work; the installer is for PROD. DEV next to PROD through Velopack was checked once and works.
  - **Data roots:** `%LOCALAPPDATA%\mordWare Data\` (PROD) and `%LOCALAPPDATA%\mordWare Data (DEV)\`, siblings of Velopack's `%LOCALAPPDATA%\<packId>\`, never inside it.
  - **Edition from a build constant** (Release = PROD), x64 only.
- **Outcome:**

  | Check | Result |
  |---|---|
  | (a) Install, upgrade, uninstall | PASS. `Setup.exe --silent` installs per user into `%LOCALAPPDATA%\<packId>\` with a per-user uninstall entry and a Start menu shortcut, unelevated. A newer `Setup.exe` upgrades in place; `Update.exe --uninstall` removes folder, entry and shortcut. |
  | (b) Data survives | PASS. The data root survives upgrade and uninstall. Anything inside the packId folder is deleted by uninstall **and by a Setup over an existing install** (it renames the folder away and installs fresh). Case-only name differences land in the same folder. |
  | (c) DEV next to PROD | PASS. Two packIds, folders, entries, shortcuts and data roots; uninstalling DEV leaves PROD and its data intact. |
  | (d) Unpackaged runtime | PASS. The Windows App SDK `Microsoft.Windows.Globalization` language override drives MRT Core `.resw` resources (en-US / it-IT); the `Windows.Globalization` one the app uses today throws unpackaged. The edition follows the build constant. From the build output, update checks turn themselves off. |
  | (e) Folder picker | PASS. `FolderPicker(AppWindow.Id)` works unpackaged; the path, kept in a file in the data root, is remembered across a restart. |
  | (f) Unsigned first run | Partly. The browser asked to keep or discard the download; the file had no Mark-of-the-Web, so SmartScreen did not run on the test PC. Documented instead: unsigned files show "Windows protected your PC" / "Run anyway", and **Smart App Control blocks unsigned executables with no per-app override** (see [v1.0.0]). |
  | (g) Local-feed update | PASS. 0.1.0 -> 0.1.1 found, nothing applied until asked; then a 0.2 MB delta, `Update.exe apply` after the app exits, restart on 0.1.1, data intact. |
  | (h) GitHub update | PASS. Two temporary `s4-feed-*` pre-releases (owner-approved, deleted after about 20 minutes). Found and applied anonymously: one API request plus small files, user agent `Velopack/1.2.161`, no token, no user data. |
  | (i) Binary public safety | PASS. With a deterministic build, `PathMap` and no PDBs, our own files hold no local path or private word. `Setup.exe` embeds the full package byte for byte. Matches in Microsoft and Velopack binaries are upstream content (byte-identical to their packages). |
  | (j) Entry point | PASS, after a fix: our own `Main` runs Velopack first, then WinUI. **`EnableMsixTooling` must stay `true`**: without it the publish output lacks the app's `.pri` and the installed window crashes. |

- **Superseded:** S3's package-only findings (LocalState, write virtualization, Reset). What still applies from S3: backups, OneDrive, atomic writes, edition binding, restore with the recovery key.

## [D] Delivery

- **Status:** done (2026-11-06). Change `delivery-unpackaged`, archived as `openspec/changes/archive/2026-11-06-delivery-unpackaged/`. Specs: `app-delivery` (new), `app-environments`, `development-workflow`, `public-repository`. **Pending:** the release dry run (task 8.4), deferred by the owner until after archive: push `main` with the owner's approval, start `release.yml` by hand (`workflow_dispatch`), check its artifact (PROD installer and full package only, notes with the license line and source link, scan report) and record the result here. **Dry run #1 (2026-11-06) failed at packing:** vpk refuses versions below 0.0.1, and the planned `0.0.0-dryrun.<run>` is a pre-release that sorts below it. Everything before packing passed (prep, build, tests) and nothing was published. Change `fix-dryrun-version` sets the dry-run version to `0.1.0-dryrun.<run>`. **Dry run #2 (2026-11-06) packed `0.1.0-dryrun.2`, but the package scan blocked it:** Velopack's vendor binaries embed their own build machine's user-folder path, which is the same path as the GitHub runner's profile, so the this-PC rule fired on upstream text in the three Velopack-derived binaries. Nothing was published. Change `fix-scan-vendor-text` lets this-PC text pass in those binaries only when the pinned vendor binary contains exactly that text; the dry run is to be run again after it.
- **Scope:**
  - **Unpackage the app:** remove the MSIX manifests and packaging; the app runs from its build output and from the installer.
  - **Editions without package identity.** The edition is fixed by the build (Release = PROD, every other configuration = DEV), with separate app ids, data folders, Start menu entries and icons. This modifies the `app-environments` spec.
  - **`run-dev.ps1` rework:** no Developer Mode or package registration needed.
  - **Release workflow:** a pushed `v*.*.*` tag that points at a commit on `main` builds, tests, packs the installer and publishes the GitHub Release. The version comes from the tag. A manual dry-run mode builds the installer as a workflow artifact without publishing anything. This extends the `development-workflow` spec.
  - **`THIRD-PARTY-NOTICES`** shipped with the installer, for every redistributed dependency's license.
  - **GPL-3.0 source offer (section 6).** Every release offers its corresponding source: the GitHub Release links the tagged source archive, and the release notes say so. The installer ships the `LICENSE` text next to `THIRD-PARTY-NOTICES`.
- **Parked notes:**
  - **Applied in change `delivery-unpackaged`** (2026-11-06), adopting the [S4] reference code and deleting `spikes/S4.Installer`:
    - **Project:** unpackaged (`WindowsPackageType=None`), self-contained .NET and Windows App SDK, `win-x64` only (`MordWare.slnx` maps the App to x64), `EnableMsixTooling=true` kept, ReadyToRun off. `Program.Main` runs `VelopackApp.Build().Run()` first.
    - **Language:** the Windows App SDK override must be set in `Program.Main`, before the XAML application starts. Set in `OnLaunched` (as the packaged app did), it no longer changed the resources' language: the it-IT window showed English.
    - **Edition:** `MORDWARE_PROD` for Release only; `EditionProfile` in Core holds packIds, titles and data folder names; the `MordWare.Edition` assembly metadata is checked per configuration by the architecture tests.
    - **Final Windows App SDK component set (2.6.5):** `Microsoft.WindowsAppSDK.Base`, `.Foundation`, `.InteractiveExperiences`, `.WinUI` and `.DWrite`. `.Runtime` is not needed for the self-contained build. WebView2 (BSD-3-Clause) comes transitively with WinUI. No `onnxruntime*`, `DirectML*` or `Microsoft.Windows.AI*` file ships; the publish folder dropped from 233 MB to 176 MB.
    - **Packing:** `scripts/pack.ps1` with `--noPortable` (no portable zip is shipped) and `--delta None` (deltas come with [U]). `Setup.exe` about 82 MB, full package about 75 MB.
    - **Package scan:** `check-public-safety.ps1 -Package`. vpk builds three Velopack binaries per app (the `Setup.exe` stub, the shortcut stub and `Squirrel.exe`), so they can never be byte-identical to upstream. Owner decision 2026-11-06: they pass only if every rule hit in them, this-PC hits included (refined by change `fix-scan-vendor-text` after dry run #2), also occurs in their vendor binary of the pinned vpk package, and they contain no private word.
    - **Upstream notices:** the Windows App SDK and .NET third-party notices contain e-mail addresses, which the repository's public-safety rule refuses. Owner decision 2026-11-06: they are not committed; `pack.ps1` copies them byte for byte from their NuGet packages into the installer (`third-party-notices\`), and the committed `THIRD-PARTY-NOTICES.txt` lists every component with its full license terms.
    - **Installer checks** (2026-11-06, Windows build 26300, x64, check-only DEV installer 0.0.1 and 0.0.2, no admin rights, no Windows setting changed): installed without elevation, with a Start menu entry "mordWare (DEV)" using the DEV icon, an installed-apps entry and no desktop shortcut; the installed window opened in en-US with the DEV marker; the installation folder held the license, the additional permission and all notices; no TCP or UDP endpoint was owned by the app during three minutes with its window open; 0.0.2 over 0.0.1 upgraded in place (one entry each, data marker unchanged); uninstalling removed the program files and both entries and kept the DEV data folder. The PROD folders were not created or touched.
  - **The live publish path is not exercised before v1.0.0.** The dry run covers every step except `publish` (the `vpk upload github` call and the release notes).
  - **Before installing PROD on any PC, `%LOCALAPPDATA%\MordWare\` must not hold anything else:** it is the installer's program folder (packId `MordWare`), and Velopack deletes it on uninstall and on a Setup over an existing install.
  - **Platforms (decided in [S4]): x64 only.** No x86 and no ARM64 build; ARM64 PCs run the x64 app under emulation, and ARM64 can be added later as one more Velopack channel.
  - **Install facts (from [S4]):** `Setup.exe --silent` does not start the app afterwards; a normal install does, once. A Setup over an existing install fails if it starts within about 2 s of the previous install or app run ("Failed to remove existing application directory"); it works a few seconds later. In-app updates are not affected.
  - **Release upload (from [S4]):** `vpk upload github` reads the token from `VPK_TOKEN` (never on the command line); `--targetCommitish` needs a full SHA or a branch name.

## [1] Foundation

- **Status:** todo (after [D])
- **Scope:**
  - **Persistence:** EF Core + SQLite per module context in one file, WAL mode, encryption, recovery key, backup/restore, safe migrations with an automatic pre-migration backup.
  - **Shell:** navigation, light/dark theme, en-US/it-IT setting, settings page.
  - **Home dashboard frame:** the app's main page, giving it its "personal household assistant" context. Each module contributes its own cards through a contract it owns (guardrail 8), so Home never references module internals.
- **Parked notes:**
  - **Live data root (decided in [S4]):** `%LOCALAPPDATA%\mordWare Data\` (PROD) and `%LOCALAPPDATA%\mordWare Data (DEV)\`. They are siblings of the installer's `%LOCALAPPDATA%\MordWare\`, never inside it: Velopack deletes that whole folder on uninstall and on a Setup over an existing install, and Windows paths ignore case. The database, the wrapped keys and the logs live in the data root; `IAppPaths` gets an unpackaged implementation over it. The backup design below stays.
  - **No `ApplicationData` and no `LocalSettings` unpackaged.** Settings (language, theme, backup folder, update check) go in a file in the data root, the way the [S4] probe keeps its picked folder. The Windows App SDK folder picker works unpackaged ([S4] (e)).
  - **Re-check the storage notes below** that were written for package LocalState (LocalState, Reset, write virtualization): they no longer apply. Replace them with the data root above when Foundation is explored.
  - **DEV/PROD separation** (guardrail 7):
    - An environment marker stored inside each database and checked at startup; a build refuses a database of the other edition.
    - Separate data keys, recovery keys and backup folders per edition.
    - Restore PROD backup -> DEV is allowed (to debug on a copy of real data, after entering the PROD recovery key, then re-wrapping under DEV). Restore DEV backup -> PROD is refused (each backup records its edition).
  - **No database compaction** in the first release (owner decision 2026-11-04); speed matters, size does not.
  - **Backups** on the first launch of the day, before every migration, and on demand ("Back up now" in Settings). Never on exit (decided in [S3]). Retention: 7 daily + 4 weekly + 6 monthly (proposed default).
  - **Language:** replace the hard-coded `AppLanguages.Default` with the user's setting.
  - **Window title:** derive it from the shell's current page (today it is just the brand).
  - **About and legal notices (GPL-3.0 section 5(d)).** Settings has an About section that shows the copyright ("Copyright (C) 2026 KingMordas"), that there is no warranty, that mordWare is licensed GPL-3.0-only, how to view the license, and where to get the source (the GitHub repository). All of it is localized in en-US and it-IT (guardrail 3).
  - **Encryption (from [S1]; reference code in `spikes/S1.Encryption`):**
    - **Adopt into the root `Directory.Packages.props`:** `SQLite3MC.PCLRaw.bundle`, `Microsoft.EntityFrameworkCore.Sqlite.Core` (never the non-`.Core` package), and `System.Security.Cryptography.ProtectedData`. Then delete `spikes/S1.Encryption` and its entries in `spikes/Directory.Packages.props`.
    - **Make the engine checks permanent tests:** `sqlite3mc_version()` answers, and the build output has no `e_sqlite3`.
    - **Connection:** URI `file:<percent-encoded path>?cipher=sqlcipher&legacy=4` plus `Password=x'<hex data key>'`.
      - Catch `SqliteException` 26 from `Open()` and show it as a localized "wrong key / not a mordWare database" message.
      - Backups go only to a destination opened the same way.
    - **Wraps are edition-bound.**
      - DPAPI sits behind an interface implemented on the Windows side, because `ProtectedData` is Windows-only (CA1416 in `net10.0` libraries).
      - Restoring PROD -> DEV unwraps as PROD, then re-wraps under DEV.
    - **Generated migrations keep their model snapshot.** The spike's `PendingModelChangesWarning` suppression exists only because its migrations were hand-written.
    - **Open question:** a check symbol for the recovery key (Crockford mod 37), so the UI can say "typo" rather than "wrong key". This belongs to the recovery UX.
  - **Storage, backup and restore (from [S3]; reference code in `spikes/S3.Storage`):**
    - **Folder layout:**
      - **Live data:** one data folder per edition, holding the database, the DPAPI-wrapped data key and the recovery-wrapped data key. ([S3] used package LocalState; [S4]/[D] set the unpackaged location.)
      - **Backups:** the user's chosen folder. The default is `Documents\mordWare Backups` or `Documents\mordWare Backups (DEV)` (`Environment.SpecialFolder.MyDocuments`, which follows a OneDrive Known Folder Move).
    - **`IAppPaths` gains the backup folder.** The choice is stored in the edition's local settings.
    - **First run** offers "New database" or "Restore from backup". A new database asks to confirm the backup folder, says whether it is synced by OneDrive, and shows the recovery key.
    - **Backup write sequence:**
      - SQLite backup API into `<name>.db.tmp` in the destination folder, keyed with the same data key
      - then header + bytes into `<name>.<ext>.tmp`, with write-through and a flush
      - then rename to `<name>.<ext>`
      - Any `.tmp` is never a backup, and the next run deletes it.
    - **Backup header** (draft proven in [S3]; finalize here):
      - magic `MWBK`, format version, edition, schema version, created UTC, the 77-byte recovery blob, SHA-256 of the payload, then the encrypted database unchanged
      - It is plaintext by design: the edition is protected by the wrap's edition binding.
      - Decide here the file name pattern and extension (the spike used `spike-PROD-<utc>.s3bak`).
    - **Restore flow**, which never touches the live database until the restored copy checks out:
      - check the header and payload hash
      - apply the edition rule: DEV -> PROD refused
      - unwrap with the recovery key under the header's edition
      - write `.restore.tmp`, open it and run `quick_check`
      - replace the live database (delete its `-wal` / `-shm` first)
      - re-wrap both blobs under the running edition
    - **Map restore failures to localized messages:** damaged or incomplete file (hash), wrong recovery key (tag mismatch), backup from the other edition, not a mordWare backup (magic).
    - **Delete `spikes/S3.Storage`** once backup and restore exist in `src/`.
  - **Decimal storage (from [S2]; reference code in `spikes/S2.Decimals`):**
    - **Shared types:** `IScaled<TSelf>`, `Money` and `MoneyAmount` go in `MordWare.Core` (vehicles, houses and finance all carry money). The generic `ScaledConverter<T>` goes in `MordWare.Persistence`. Each module's own quantities (`Km`, `Liters`, `Kwh`, ...) live in its `Domain`, with no EF reference.
    - **Every decimal property is mapped through `ScaledConverter<T>`.** Never map a raw `decimal`: EF would store TEXT. A permanent test could assert that no mapped property has CLR type `decimal`.
    - **Money columns are a pair** `XxxAmount` + `XxxCurrency`, indexed `(Currency, Amount)` where queried. Avoid complex properties for it.
    - **Name raw columns for their scale where a raw look matters**, or document the scale per column in `docs/architecture.md`, because DB Browser shows `1234510` for 12,345.10 km.
    - **Map unique-index violations** (`SqliteException` extended code 2067) to a localized "already exists" message.
    - **Delete `spikes/S2.Decimals`** once the types and converter are adopted into `src/`.

## [U] Update check at startup

- **Status:** todo (after Foundation, which provides the Settings page)
- **Scope:**
  - At startup, check GitHub Releases for a newer version, using the update mechanism chosen in [S4].
  - On by default, with a Settings switch to turn it off.
  - When a newer version exists, offer it in-app (localized, guardrail 4); download and install only after the user clicks "Update". Never silent.
  - The request carries nothing about the user (guardrail 2) and a failed check is silent apart from a log line without personal data.
- **Parked notes:**
  - Test it against a local test feed with two locally built versions, since no GitHub release exists before v1.0.0. `SimpleFileSource` over a folder behaves like GitHub ([S4] (g)).
  - **Update API (from [S4]):** `UpdateManager(new GithubSource(<repository URL>, accessToken: null, prerelease: false), options)` with the default channel. `CheckForUpdatesAsync`, then show the offer, then `DownloadUpdatesAsync` (it prefers deltas) and `ApplyUpdatesAndRestart`. When `IsInstalled` is false (DEV from the build output) there is no check.
  - **What a check sends:** one GitHub API request for the release list, then one small feed file per listed release, with the user agent `Velopack/<version>`, an `Accept` header and nothing else: no token, no user or PC data (guardrail 2). The anonymous limit is 60 API requests per hour per IP, far above one check per start.
  - **Logs (guardrail 6):** Velopack's log messages contain full paths with the Windows user name. Do not forward them to mordWare's log as they are: lower the level, or replace the user-profile prefix first.
  - `UpdateManager.Channel` is not public in Velopack 1.2.161; the channel is known from the build.

## [2] morVehicles

- **Status:** todo
- **Scope:** garage -> refuel -> stats -> maintenance. Same features as the earlier web version, **new UI and UX**, plus the module's Home dashboard cards.
- **Parked notes:**
  - **Refuels stand alone** with amount and currency (Finance does not exist yet). `IExpenseBooking` is defined by Finance's contract, not implemented; booking a cost is unavailable until Finance exists.
  - **Storage (from [S2]):** `Km` (x100) and `Liters` (x1000) in the module's `Domain`; refuel and maintenance costs as `Money` (a `CostAmount` + `CostCurrency` pair). Queries compare domain values directly (`r.OdometerKm > from`), and stats load rows and compute in C# (13 ms for 10k refuels in [S2]).
  - **Open (from [S2]):** how many decimals a price per liter shows (for example 3 for fuel, as on pump displays). Storage at x10^4 supports any choice up to 4.
  - **Charting:** LiveCharts2 (MIT, WinUI support) proposed over ScottPlot.
  - **Port references** are kept in the owner's local notes, outside this repository. Specs describe mordWare's behavior on its own terms.

## [3] Importer

- **Status:** todo
- **Scope:** a one-time import of the owner's existing data into mordWare (DEV first, checked, then PROD).
- **Parked notes:**
  - **A private, owner-only tool kept outside this repository.** It is not a shipped feature. Its design and code stay private because they would describe the earlier web app (guardrail 11).
  - It writes a mordWare database through mordWare's own persistence and types, which refuse any out-of-range value rather than truncate it.

## [4] morHouses

- **Status:** todo
- **Scope:** houses and their utilities (electricity and gas bills). Same features as the earlier web version where they exist, **new UI and UX**, plus Home dashboard cards.
- **Parked notes:**
  - **House** is a cross-module entity in Core. Booking bills to Finance is optional.
  - **"House in use" is a contract.** Before a house is deleted, Core asks every module that references houses; each module answers. No module needs to be listed by hand.

## [5] morFinance

- **Status:** todo (largest; last)
- **Scope:** same features as the earlier web version, **new UI and UX**, plus Home dashboard cards. Writing its specs is part of the work.
- **Parked notes:**
  - **Implements `IExpenseBooking`.** It relinks imported refuels and maintenance to their transactions.
  - FX rates come from a free public API (Frankfurter), in Integrations; calls carry no user data (guardrail 2).
  - **Rounding mode (open, from [S2]):** half-even vs. half-away-from-zero for calculations such as FX conversion, interest and splits. [S2] made rounding always explicit (`MidpointRounding` is a required argument), so the choice is made here. Signed amounts vs. positive magnitudes is also decided here; the storage supports both.

## [v1.0.0] First release

- **Status:** todo
- **Scope:** Vehicles, Houses and Finance complete and working. Push the tag `v1.0.0` on `main`; the release workflow from [D] publishes the installer on GitHub Releases. There are no releases before this one.
- **Parked notes:**
  - **Owner decision before v1.0.0: Smart App Control vs. no code signing** (moved here from [D]). Microsoft documents that Smart App Control (Windows 11) blocks unsigned executables unless they already have a positive reputation, and that "there is currently no way to bypass Smart App Control protection for individual apps"; it checks every executable, not only downloads. Every unsigned release starts again at zero reputation. So on PCs where it is on, an unsigned mordWare will most likely not run at all. Options to weigh: stay unsigned and document it; sign (for example with Artifact Signing, a paid service that needs an identity check, at odds with the free, no-paid-tiers stance of guardrail 1); or a Microsoft Store edition. The README's Smart App Control sentence is marked provisional until this is decided. Sources: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation and https://support.microsoft.com/en-us/windows/security/threat-malware-protection/smart-app-control-frequently-asked-questions
  - **Review the Microsoft license reading before the first release** (from [D]). mordWare redistributes the Windows App SDK components and the Windows SDK projection under Microsoft's license terms, with the GPL-3.0 section 7 additional permission in `LICENSE-EXCEPTION.txt`. Microsoft's distribution condition 3b.ii (end users agree to terms that protect Microsoft at least as much) is handled by shipping the full terms in `THIRD-PARTY-NOTICES.txt` together with a README statement. This is a good-faith reading, not legal advice; a commercial or Store edition would get proper advice first. The projection's terms are the Windows SDK license (https://aka.ms/WinSDKLicenseURL); confirm that it lists the projection as distributable code.
  - **The release workflow's publish job** runs for the first time with the `v1.0.0` tag: watch it, and check the release page (installer, full package, notes with the license line and source link).

## [L] Later / unscheduled

- **Project website:** after v1.0.0, the project website becomes mordWare's front page (downloads link to GitHub Releases).
- **Free code signing:** if SmartScreen friction matters, apply to a free OSS signing program (for example SignPath Foundation). Not a goal.
- **Adapt `/ship`** (branch + PR + squash) when the owner reintroduces PRs.
- **Phone inbox spike** (after refuel): an iOS Shortcut saves one JSON file per entry (with type and format version) into OneDrive or iCloud Drive; mordWare reviews and imports it. First check whether Shortcuts "Save File" writes reliably to OneDrive.
- **UI verification:** a FlaUI (MIT) screenshot harness for en/it x light/dark, replacing manual screenshots.
- **In-app user guide:** decide after morVehicles.
