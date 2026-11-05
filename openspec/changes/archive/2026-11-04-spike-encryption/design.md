# Design

## Context

- **Persistence is still empty.**
  - `MordWare.Persistence` is an empty `net10.0` skeleton.
  - Central package management (`Directory.Packages.props`) holds only the app's and the tests' packages.
  - `MordWare.slnx` lists `src/` and `tests/`; CI builds and tests exactly that solution.
  - The architecture tests load every `MordWare.*` assembly.
- **Market facts verified on 2026-11-04 during explore:**
  - SQLitePCLRaw 3.0 deprecated `bundle_e_sqlcipher` and removed `bundle_e_sqlite3mc`.
  - `SQLite3MC.PCLRaw.bundle` (MIT, utelle; 2.6.1 on 2026-10-12) depends on `SQLitePCLRaw.core >= 3.0.2`.
  - `Microsoft.Data.Sqlite.Core` 10.0.x declares `SQLitePCLRaw.core >= 2.1.13`. Microsoft moves to 3.0 only with 11.0.
  - SQLite3MC documents that the `Password` connection-string keyword works again on .NET 10.
  - SQLite3MC 2.6.0/2.6.1 has a known ChaCha20 issue, which doesn't affect the SQLCipher scheme.

See proposal.md for why the spike exists and what it must prove.

## Goals / Non-Goals

**Goals:**
- Each success criterion becomes a test that passes or fails without anyone reading code. A failure is a valid spike result and gets recorded, not hidden.
- Foundation can copy the proven configuration and key chain without making a new decision.

**Non-Goals:**
- Production-quality abstractions: no `IKeyStore` and no DI wiring.
- A key-store file format, a backup container, or a recovery UX.
- Any WinUI code.

## Decisions

### D1. The spike is an isolated test project outside the solution

```
spikes/
  Directory.Packages.props      imports the root file, adds spike-only versions
  S1.Encryption/
    S1.Encryption.csproj        net10.0, xUnit v3 (same runner as tests/), NOT in MordWare.slnx
    ...tests per D2-D8
```

- **Isolation.**
  - Not in `MordWare.slnx`, so CI and the app build never compile it.
  - Named `S1.Encryption`, not `MordWare.*`, so the architecture tests never load it.
  - Its own `Directory.Packages.props` imports the root one and adds the spike's versions, so the app's central versions stay untouched until Foundation adopts them on purpose.
  - The root `Directory.Build.props` still applies (nullable, warnings as errors).
- **Lifetime.** The project stays in the repository as Foundation's reference implementation. Foundation deletes it when it moves the code into `MordWare.Persistence` (recorded in the roadmap).
- **Temporary data.** Every test works in its own temporary folder, deleted when the test ends. A small local helper does this; it doesn't reference `tests/` because the spike must stay standalone.
- **Alternatives rejected:**
  - A console app: its result is printed, not asserted, and nothing re-checks it.
  - Code in `MordWare.Persistence`: it would pull unproven packages into the app's graph and central versions.

### D2. Package graph: one bundle, SQLite3MC

```
S1.Encryption
  +-- Microsoft.EntityFrameworkCore.Sqlite.Core 10.0.x   (MIT)  no bundle of its own
  |      +-- Microsoft.Data.Sqlite.Core 10.0.x --> SQLitePCLRaw.core >= 2.1.13
  +-- SQLite3MC.PCLRaw.bundle (latest stable, pinned)    (MIT)
  |      +-- SQLite3MC.PCLRaw.provider --> SQLitePCLRaw.core >= 3.0.2   => resolves 3.x
  |      +-- SQLite3MC.PCLRaw.lib (native sqlite3mc)
  +-- System.Security.Cryptography.ProtectedData 10.0.x  (MIT)  DPAPI
```

- **Proof that the encrypted engine is the one running.** A test asserts that `SELECT sqlite3mc_version()` succeeds, which only SQLite3MC's build provides. A second test asserts that the build output contains no plain `e_sqlite3` native library.
  - Together they guard against a second bundle being picked up silently. Foundation turns this into a permanent test.
- **SQLitePCLRaw.core version.** The test records the resolved version in the task notes.
- **Migrations.**
  - The spike's migration is written by hand, a `Migration` class with its attribute. That avoids `Microsoft.EntityFrameworkCore.Design`.
  - The test runs `Database.Migrate()` twice (create, then a second additive migration) and checks `__EFMigrationsHistory`. This proves the whole EF Core path on SQLitePCLRaw 3.x, not just `SqliteConnection`.

### D3. Cipher: SQLCipher v4 scheme, chosen per connection

- **Connection string.** `Data Source=file:<path>?cipher=sqlcipher&legacy=4;Password=<key>`
  - URI parameters select the cipher before the key is applied, the way SQLite3MC's documentation recommends.
  - The path is percent-encoded in the URI. A test uses a temporary path with a space in it to prove this.
- **Why not the default sqleet (ChaCha20-Poly1305) scheme.** SQLCipher v4 is the most widely readable encrypted SQLite format, so a backup stays openable with standard tools if SQLite3MC or mordWare ever goes away. It is also unaffected by the 2.6.x ChaCha20 issue.
- **WAL.** The tests run in `journal_mode=WAL`, Foundation's mode. A test writes a known marker string, checkpoints, and checks that the marker appears in none of the bytes of the `.db`, `-wal` or `-shm` files.

### D4. The data key is a raw 256-bit key

- **The data key.** 32 bytes from `RandomNumberGenerator`, passed to SQLCipher as a raw key `x'<64 hex>'`, so SQLCipher's PBKDF2 (256,000 iterations) is skipped on every open. The key is already full-entropy; stretching it would add only latency.
- **First choice:** the raw key goes through the `Password` keyword.
- **Fallbacks, in order, if the first choice fails.** The spike stops at the first that works and records which.
  1. Open without `Password` and run `PRAGMA key = "x'...'"` as the first statement, through EF Core's connection-opened hook.
  2. Pass the hex string as a passphrase with the default KDF. Use this only if the measured open time is acceptable (D8).
- **Refusal.** A database created with key A must fail to open with key B, and with no key, raising a clear error rather than returning garbage. The tests assert the exception type and record it, because Foundation maps it to a localized message.

### D5. Key chain: DPAPI for daily use, HKDF + AES-GCM for recovery

```
                 data key (DEK, 32 random bytes)
                 /                               \
     DPAPI Protect(CurrentUser,                   AES-256-GCM encrypt
       entropy = "mordWare|<edition>|dek|v1")       key   = HKDF-SHA256(ikm  = recovery key,
                 |                                                   salt = 16 random bytes,
                 v                                                   info = "mordWare recovery-wrap v1",
       DPAPI blob  (LocalState, daily use)                           len  = 32)
                                                    nonce = 12 random bytes
                                                    aad   = "mordWare|<edition>|v1"
                                                          |
                                                          v
                                         recovery blob = v1 | salt | nonce | ciphertext | tag
                                         (travels with every backup)
```

- **Only .NET base-library and Microsoft crypto:** `RandomNumberGenerator`, `HKDF`, `AesGcm`, `ProtectedData`. There is no third-party crypto.
- **Why HKDF and not Argon2id.** The recovery key is generated with at least 128 bits of entropy and is never user-chosen (D6), so brute force is impossible regardless of KDF cost. A memory-hard KDF would only add a dependency.
  - Constraint for the future: a user-chosen passphrase would require revisiting this. It is out of scope.
- **The edition is bound into both wraps** (DPAPI entropy and AES-GCM associated data).
  - A recovery blob made for PROD fails to unwrap when the caller claims DEV, and vice versa. A test proves it.
  - This supports guardrail 7. The roadmap's "PROD backup -> DEV allowed" restore still works because the restore flow states the backup's own edition, unwraps, and then re-wraps under DEV. The DEV -> PROD refusal is a policy enforced by Foundation, not by this cryptography.
- **Round-trip test:**
  1. Create a DEK and an encrypted database.
  2. Wrap the DEK with DPAPI and with a new recovery key.
  3. Discard the DEK.
  4. Unwrap via DPAPI and open the database.
  5. Unwrap via the recovery key and open the database.
  6. A wrong recovery key and a tampered blob both fail (AES-GCM tag mismatch).
- **Hygiene.**
  - Key byte arrays are cleared with `CryptographicOperations.ZeroMemory` after use where practical.
  - The hex key inside the connection string is a managed string and can't be cleared. That is accepted: malware running as the user is outside the threat model, since it could call DPAPI anyway.

### D6. Recovery-key format

- **Alphabet.** Crockford base32 (`0-9`, `A-Z` without `I L O U`). That gives 30 symbols drawn with `RandomNumberGenerator.GetInt32(32)` = **150 bits**.
- **Display.** Six groups of five, for example `7KQ2M-...`.
- **Parsing is forgiving:**
  - case-insensitive
  - spaces and hyphens ignored
  - `I`/`L` read as `1`, `O` read as `0`
- **HKDF input.** The canonical 30-character string is the HKDF input key material.
- **Tests:** generate -> format -> parse round-trips, lowercase and ambiguous characters normalize, and a wrong length is rejected.
- **Why 30 characters and not 26.** 26 characters (130 bits) don't split into equal groups, and 150 bits costs four more characters.

### D7. Backup via SQLite's online backup API

- **The test:**
  1. Fill an encrypted database (WAL, with live connections).
  2. Run `SqliteConnection.BackupDatabase` into a destination connection opened with the same cipher URI and key.
  3. Close everything.
  4. Open the copy with the key and verify the content.
  5. Verify the copy fails to open without the key and contains no plaintext marker.
- **Why the backup API.** It is the method Foundation will use for its daily and pre-migration backups, because it takes a consistent snapshot without stopping the app.
- **If the destination must be keyed differently** (for example, SQLite3MC refuses a mismatched cipher configuration), the spike records the rule it found.

### D8. Measurements and the manual tool check

- **Measured with `Stopwatch`** and written to the test output and the task notes, never a key:
  - the median cold open plus first query over 20 opens, for the raw key and for the passphrase-with-KDF variant
  - inserting 10,000 rows in one transaction, and reading them back, encrypted vs. an unkeyed database on the same engine
- **Guideline.** An encrypted open under about 50 ms and encryption overhead under 2x. Exceeding either is reported for a decision, not silently accepted.
- **Manual check.**
  - An explicit (opt-in) test writes a sample database to `%TEMP%\mordware-s1-manual\` and prints its throwaway key.
  - The owner opens it in DB Browser for SQLite (SQLCipher 4 defaults, raw key `0x...`).
  - The folder is deleted afterwards.
  - The sample holds only synthetic data, so printing that key breaks no guardrail.

### D9. Edition separation (guardrail 7)

- **The spike touches neither edition.** It uses no `IAppPaths`, no LocalState and no package identity, and it writes only to temporary folders, including the manual-check folder in `%TEMP%`, which is deleted afterwards.
- **DPAPI is safe here.** The DPAPI test protects only throwaway keys under the current user, so it cannot affect any real database.
- **What Foundation inherits:** the edition-bound wraps from D5, and separate DEKs and recovery keys per edition.

## Risks / Trade-offs

- **[Microsoft.Data.Sqlite 10 is binary-incompatible with SQLitePCLRaw.core 3.x]** -> This is test 1 and is checked first.
  - If it fails, the spike stops and the outcome is recorded.
  - Options for a return to explore: the deprecated `SQLitePCLRaw.bundle_e_sqlite3mc` 2.1.x (unmaintained, so a stopgap at best), or waiting for or targeting Microsoft.Data.Sqlite 11.
  - Nothing is forced through.
- **[Single maintainer for SQLite3MC]** -> The SQLCipher v4 format keeps the data readable by other SQLCipher builds, so the risk is to the library, not to the data.
- **[A second SQLitePCLRaw bundle sneaks in and opens an unencrypted database]** -> D2's engine check and the no-`e_sqlite3` check. Foundation makes them permanent.
- **[The raw key doesn't pass through `Password`]** -> D4's ordered fallbacks.
- **[DPAPI keys are lost]** (Windows reinstall, profile reset, another PC) -> This is exactly what the recovery key is for. The round-trip test proves the recovery path opens the database without DPAPI.
- **[The ChaCha20 issue in SQLite3MC 2.6.x]** -> Doesn't apply to the SQLCipher scheme. The pinned version and its known issues are recorded anyway.
- **[Key material lingers in managed strings]** -> Accepted (D5). It is outside the threat model.

## Migration Plan

Not applicable: no app code changes and no user data exists. Rollback means deleting `spikes/`.

## Open Questions

- **A check symbol for the recovery key** (Crockford's mod-37 check) would let the UI say "typo" rather than "wrong key". It is deferred to Foundation's recovery UX and doesn't affect the spike's key chain.
- **Where the DPAPI blob and recovery blob are stored** (a key file next to the database, a backup header) is decided by S3 and Foundation. The spike handles the blobs in memory only.
