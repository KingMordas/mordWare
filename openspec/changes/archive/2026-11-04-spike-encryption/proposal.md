# Proposal

## Why

Foundation needs an encrypted SQLite database with a recovery key (roadmap [1]), and guardrail 1 allows only free, OSS-licensed dependencies. The 2026-11-04 explore of roadmap item [S1] found that the free options have changed:
- SQLitePCLRaw 3.0 stopped shipping free encryption builds. `bundle_e_sqlcipher` is deprecated and `bundle_e_sqlite3mc` was removed.
- That leaves SQLite3 Multiple Ciphers (MIT) as the only free candidate.
- SQLite3 Multiple Ciphers depends on SQLitePCLRaw.core 3.x, while EF Core 10's Microsoft.Data.Sqlite was built against 2.1.x.

That combination, and the key chain designed around it, must be proven in throwaway code before Foundation builds on it. Finding out after modules hold real data would be expensive.

## What Changes

- **A throwaway spike project, `spikes/S1.Encryption`.** It is an xUnit test project that is not part of `MordWare.slnx` and is never referenced by the app. Its tests prove or disprove each decision below.
- **Decisions the spike validates** (made in explore, recorded in design.md):
  - **Library:** `SQLite3MC.PCLRaw.bundle` (MIT) with `Microsoft.EntityFrameworkCore.Sqlite.Core`, so the graph holds exactly one SQLitePCLRaw bundle.
  - **Cipher:** the SQLCipher v4 scheme (AES-256-CBC + HMAC-SHA512), chosen so the data stays readable by standard SQLCipher tools.
  - **Data key:** a random 256-bit data key used as a raw key, so no key derivation runs on every open.
  - **Daily unlock:** DPAPI (CurrentUser) wraps the data key.
  - **Recovery:** a generated recovery key of at least 128 bits, never user-chosen, wraps the data key through HKDF-SHA256 + AES-256-GCM. Both come from the .NET base library.
  - **Argon2id is dropped from the design.** A KDF built to slow down password guessing adds nothing for a random key of 128+ bits, and dropping it avoids a third-party crypto dependency.
- **Recorded outcome:**
  - Pass or fail per success criterion, plus the measured open time, in the task notes and in `docs/roadmap.md` ([S1] done, its notes rewritten; Foundation's notes get the decided stack).
  - `docs/architecture.md` describes the `spikes/` folder.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. A spike changes no behavior of the app, so this change sets `skip_specs: true`. The encryption behavior (unlock, recovery, refusal of the wrong key) gets its specs in Foundation, where it becomes real.

## Scope

**In:**
- The `spikes/S1.Encryption` test project, with its package versions isolated from the app's central versions.
- Proof of the EF Core 10 + SQLitePCLRaw.core 3.x + SQLite3MC combination: open, read, write, migrate.
- Refusal to open with a wrong key or no key.
- The raw-key and SQLCipher v4 configuration, and the measured open time.
- A backup via SQLite's online backup API that restores and opens.
- A manual check that a spike database opens in DB Browser for SQLite (SQLCipher 4) with the raw key.
- The key round trip: data key -> DPAPI wrap and unwrap, recovery wrap and unwrap -> the database opens.
- Generating and parsing the recovery-key format.
- Docs: `docs/roadmap.md` and `docs/architecture.md`.

**Out:**
- **Left to Foundation:**
  - Any production code in `src/` or `tests/`.
  - Any change to `MordWare.slnx`, to the app's central package versions, or to the architecture tests.
  - The key-store file format, the backup container format and retention.
  - The recovery-key UX (show once, re-type groups), the in-database edition marker, and migrations.
- **Left to S3:** storage locations and uninstall behavior.
- **Not planned:** changing the recovery key, and any user-chosen passphrase.

## Guardrails touched

- **1 (OSS only):**
  - SQLite3MC.PCLRaw.bundle and System.Security.Cryptography.ProtectedData are both MIT.
  - EF Core and Microsoft.Data.Sqlite are MIT.
  - DB Browser for SQLite (MPL-2.0 / GPL-3.0) is used only as an external verification tool, never as a dependency.
- **2 (no telemetry):** no network calls.
- **6 (no personal data in logs):** the spike writes no key or key material to output, only pass/fail and timings.
- **7 (DEV and PROD never mix):**
  - Every spike database and key file lives in a temporary folder and is deleted afterwards.
  - The DPAPI test protects only throwaway keys.
  - Nothing touches either edition's LocalState.
  - The design binds the edition into the recovery wrap, so a key wrapped for one edition fails to unwrap as the other.
- **9 (performance):** open time is measured.

## User-visible

No. This is an internal spike, so there is no CHANGELOG entry.

## Roadmap

Item **[S1] Spike: encryption library**. While applying, its status moves to `in progress` and then `done` with this change's name.

## Impact

- **New, isolated content:** `spikes/S1.Encryption`, plus a `spikes/` package-version file that imports the root one. The app, `src/`, `tests/` and CI are unaffected.
- **New dependencies, spike only:** `SQLite3MC.PCLRaw.bundle`, `Microsoft.EntityFrameworkCore.Sqlite.Core` (migrations are hand-written, so no `Design` package), and `System.Security.Cryptography.ProtectedData`.
- **Changed docs:** `docs/roadmap.md` ([S1] and [1] notes), `docs/architecture.md` (the `spikes/` folder).
