# Tasks

## 1. Spike scaffolding

- [x] 1.1 Mark roadmap item [S1] `in progress` with change `spike-encryption` in `docs/roadmap.md`. Verify the table and the [S1] section both show it.
- [x] 1.2 Add `spikes/Directory.Packages.props`. It imports the root file and adds the spike-only versions: `Microsoft.EntityFrameworkCore.Sqlite.Core` 10.0.x, the latest stable `SQLite3MC.PCLRaw.bundle` (pinned), and `System.Security.Cryptography.ProtectedData` 10.0.x (design D1, D2).
  - Confirm each license is MIT on its NuGet page.
  - Verify the root `Directory.Packages.props` is unchanged (`git diff --stat`).
- [x] 1.3 Create `spikes/S1.Encryption/S1.Encryption.csproj` (net10.0, xUnit v3, not added to `MordWare.slnx`) with a temp-folder helper that deletes its folder on dispose.
  - Verify `dotnet test --project spikes/S1.Encryption` runs one placeholder test green.
  - Verify `dotnet build MordWare.slnx` is unaffected.
- [x] 1.4 Describe the `spikes/` folder in `docs/architecture.md` (solution layout: outside the solution, its own package versions, not `MordWare.*`, throwaway and deleted by the item that adopts it). Verify the section matches the files created in 1.2 and 1.3.

## 2. Engine and EF Core compatibility (go/no-go first)

- [x] 2.1 Add an engine test: `SELECT sqlite3mc_version()` succeeds, the resolved `SQLitePCLRaw.core` version is 3.x, and the build output has no plain `e_sqlite3` native library (design D2).
  - Verify with `dotnet test --project spikes/S1.Encryption --filter-class "*Engine*"`.
  - Record the versions in the task notes.
  - Result (2026-11-04): PASS. Resolved `SQLite3MC.PCLRaw.bundle` 2.6.1 ("SQLite3 Multiple Ciphers 2.6.1", SQLite 3.54.0), `SQLitePCLRaw.core` 3.0.2, `Microsoft.Data.Sqlite.Core` / `Microsoft.EntityFrameworkCore.Sqlite.Core` 10.0.14. The output holds only `runtimes/win-{x64,x86,arm64}/native/sqlite3mc.dll`, with no `e_sqlite3`.
- [x] 2.2 Add an EF Core test: a DbContext on an encrypted database (SQLCipher v4 URI, design D3) applies two hand-written migrations, writes and reads rows, and `__EFMigrationsHistory` lists both migrations.
  - Verify with `--filter-class "*EfCore*"`.
  - **If 2.1 or 2.2 fails because Microsoft.Data.Sqlite 10 is incompatible with SQLitePCLRaw.core 3.x, stop:** record the error in the task notes and ask the owner before going on (design, Risks).
  - Result (2026-11-04): PASS, so the go/no-go is GO. Microsoft.Data.Sqlite 10.0.14 works on SQLitePCLRaw.core 3.0.2: two hand-written migrations applied, rows written and read back, a second `Migrate()` applied nothing. Hand-written migrations without a model snapshot need `PendingModelChangesWarning` ignored; Foundation uses generated migrations with a snapshot and keeps the warning on.
- [x] 2.3 Add a test that the URI path is percent-encoded, using a temp path containing a space. Verify with `--filter-class "*Uri*"`.
  - Result (2026-11-04): PASS. `new Uri(fullPath).AbsoluteUri` + `?cipher=sqlcipher&legacy=4` encodes spaces and `#`; the database is created at the real path and reopens.

## 3. Cipher, raw key and refusal

- [x] 3.1 Add raw-key tests: a 32-byte data key passed as `x'<hex>'` through `Password` opens the database.
  - If it doesn't, apply the fallbacks of design D4 in order and record which one worked.
  - Verify with `--filter-class "*RawKey*"`.
  - Result (2026-11-04): PASS with the first choice, so no fallback was needed. `Password=x'<64 hex>'` through `SqliteConnectionStringBuilder` creates a database whose header is not `SQLite format 3`. It reopens with the same key in lower-case hex, which proves SQLCipher treats it as raw key bytes and not as a passphrase.
- [x] 3.2 Add refusal tests: a database created with key A fails to open with key B and with no key. Record the exception type and message in the task notes.
  - Verify with `--filter-class "*Refusal*"`.
  - Result (2026-11-04): PASS. A wrong key and no key both raise `SqliteException` with `SqliteErrorCode` 26 (SQLITE_NOTADB), message "SQLite Error 26: 'file is not a database'.". With a wrong key the exception comes from `Open()` itself, so Foundation catches it there. A refused attempt leaves the file intact and the right key still opens it.
- [x] 3.3 Add an at-rest test in WAL mode: write a known marker, checkpoint, and assert that the marker appears in none of the bytes of the `.db`, `-wal` and `-shm` files (design D3).
  - Verify with `--filter-class "*AtRest*"`.
  - Result (2026-11-04): PASS. In WAL mode, 500 rows carrying the marker are absent from the `.db`, `-wal` and `-shm` bytes, both before and after `wal_checkpoint(TRUNCATE)` and after close. A control test shows the scan does find the marker in an unkeyed database. Note: this SQLite3MC build has no `generate_series`; the test uses a recursive CTE.

## 4. Key chain and recovery key

- [x] 4.1 Add the recovery-key generator and parser (Crockford base32, 30 symbols, six groups of five, forgiving parse, design D6) with tests:
  - generate -> format -> parse round-trips
  - lowercase and `I/L/O` normalize
  - a wrong length is rejected
  - Verify with `--filter-class "*RecoveryKey*"`.
  - Result (2026-11-04): PASS (8 tests). 200 generate -> format -> parse round-trips. `iOabc defgh LjkmN ...` normalizes to the canonical form. 25 or 31 symbols, `U` and punctuation are rejected.
- [x] 4.2 Add the DPAPI wrap (CurrentUser, edition-bound entropy) and the recovery wrap (HKDF-SHA256 + AES-256-GCM, edition in the associated data, versioned blob, design D5), with round-trip tests that each unwrap the data key and open the database. Also test that each of these fails:
  - a wrong recovery key
  - a tampered blob
  - a PROD blob unwrapped as DEV
  - Verify with `--filter-class "*KeyChain*"`.
  - Result (2026-11-04): PASS (10 tests).
    - With the data key zeroed, the DPAPI unwrap and the recovery unwrap (key typed back in lower case) both open the database.
    - These all fail with `CryptographicException`: a wrong recovery key, a flipped bit in the salt, nonce, ciphertext or tag, an unknown blob version, a PROD blob unwrapped as DEV (and the reverse), and a PROD DPAPI blob unprotected as DEV.
    - The recovery blob is 77 bytes (version 1 | salt 16 | nonce 12 | ciphertext 32 | tag 16), with a fresh salt and nonce on every wrap.
    - `ProtectedData` needs `[SupportedOSPlatform("windows")]` in a plain `net10.0` project (CA1416 with warnings as errors). Foundation keeps DPAPI behind an interface implemented on the Windows side.
- [x] 4.3 Check that no test writes key bytes, the hex key or the recovery key to output, except the opt-in manual test in 5.2. Verify by reading the test output of a full spike run.
  - Result (2026-11-04): PASS. The only `WriteLine` calls print engine versions and exception codes and messages. A detailed run of all 32 tests has no 64-hex string, and the only recovery-key-shaped strings are the fixed `Malformed_input` theory literals.

## 5. Backup, measurements and the manual tool check

- [x] 5.1 Add a backup test (design D7): fill a database in WAL mode with an open connection and back it up with `SqliteConnection.BackupDatabase` to a destination keyed the same way. The copy must:
  - open with the key and show the same content
  - fail without the key
  - contain no plaintext marker
  - Verify with `--filter-class "*Backup*"`.
  - Record any keying rule the backup needs.
  - Result (2026-11-04): PASS (3 tests). Source in WAL mode, open, with un-checkpointed WAL content:
    - **Same key:** the copy opens, has all 1000 rows, passes `integrity_check`, fails without a key, and contains no marker.
    - **Different key:** the copy is re-encrypted under the destination's key and refuses the source key. A backup can therefore carry its own key if Foundation or S3 wants one.
    - **Unkeyed destination:** refused with "SQLite Error 1: 'backup is not supported with incompatible source and target databases'.", and no plaintext reaches the disk.
    - **Keying rule:** the destination connection must be opened with the SQLCipher URI and a key before `BackupDatabase`.
- [x] 5.2 Add the measurements (design D8):
  - median cold open plus first query over 20 opens, for the raw key and the passphrase-with-KDF variant
  - inserting and reading 10,000 rows, encrypted vs. unkeyed
  - Verify by running `--filter-class "*Measure*"` and recording the numbers in the task notes. Flag any number outside the D8 guideline for an owner decision.
  - Result (2026-11-04, Release build, this PC, two runs):
    - **Open + first query (median of 20):** raw key 0.32-0.36 ms; passphrase through SQLCipher v4's PBKDF2 90.4 ms. The raw key skips the KDF, which the test asserts.
    - **10,000 rows in one transaction, WAL, best of 3:** insert 16.1 ms unkeyed vs 17.1-17.3 ms encrypted (x1.06-1.08); read back 2.7-2.9 vs 3.1-3.2 ms (x1.09-1.15).
    - Everything is inside the guideline (<50 ms open, <2x overhead), so there is nothing to flag.
- [x] 5.3 Add the opt-in manual-check test that writes a synthetic sample to `%TEMP%\mordware-s1-manual\` and prints its throwaway raw key.
  - The owner opens the sample in DB Browser for SQLite (SQLCipher 4 defaults, raw key) and confirms the rows are readable.
  - Afterwards, delete the folder and record pass or fail in the task notes.
  - Result (2026-11-04): PASS. The owner opened `sample.db` in "DB Browser for SQLCipher" 3.13.1 (SQLCipher 4 defaults, raw key) and saw the 3 synthetic rows.
    - The first attempt failed with "file is not a database". That came from how the file was opened, not from the format: the plain "DB Browser for SQLite" shortcut can't read encrypted files.
    - A direct probe through DB Browser's bundled `sqlcipher.dll` ("4.6.1 community") opened both the raw-key sample and a passphrase twin, which the test now also writes.
    - Folder deleted.

## 6. Outcome and docs

- [x] 6.1 Rewrite the [S1] section of `docs/roadmap.md` with the outcome:
  - library, cipher, raw-key path, key chain and recovery-key format
  - pinned versions and measured numbers
  - Argon2id dropped
  - Mark [S1] `done` with this change.
  - Verify the outdated notes (SQLCipher bundle candidate, Argon2id recovery key) are gone.
- [x] 6.2 Update the [1] Foundation notes in `docs/roadmap.md` with what it inherits:
  - the decided packages, to be adopted into the central versions
  - a permanent engine/no-second-bundle test
  - edition-bound wraps
  - the open questions from design.md (recovery-key check symbol, where the blobs are stored)
  - deleting `spikes/S1.Encryption` once adopted
  - Verify each design.md open question appears there.
- [x] 6.3 Run the whole spike (`dotnet test --project spikes/S1.Encryption`), `dotnet build MordWare.slnx`, the architecture tests (`dotnet test --project tests/MordWare.Tests.Architecture`), and `openspec validate spike-encryption --strict`. Verify all four succeed and that no temp folder remains.
  - Result (2026-11-04): PASS. Spike: 37 passed and 1 skipped (the opt-in manual check). `MordWare.slnx` builds with 0 warnings and 0 errors. Architecture tests 5/5. `openspec validate spike-encryption --strict` is valid. No `mordware-s1*` folder remains in `%TEMP%`.

## Workflow follow-up

- Run /opsx:archive. There are no delta specs (`skip_specs`), so archive moves the change, confirms [S1] is `done` in `docs/roadmap.md`, and ends with one Conventional Commit on `main`.
- No CHANGELOG entry: the change is not user-visible.
