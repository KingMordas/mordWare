# Proposal

## Why

Foundation and morVehicles store money, odometer readings and liters, and later modules add rates, FX rates, energy readings and pantry quantities. EF Core's SQLite provider stores `decimal` as TEXT, which SQLite compares and sorts as strings. That breaks range filters, ordering and uniqueness, or forces EF into untranslatable queries or full scans.

The 2026-11-04 explore of roadmap item [S2] settled the approach: scaled 64-bit integers carried by domain value types. One thing is still unproven: whether EF Core 10 can query those value types (range filters, ordering, uniqueness) through value converters, on the encrypted connection from [S1], with the index still used. That answer shapes every module's `Data` layer, so it must be proven in throwaway code before Foundation.

## What Changes

- **A throwaway spike project, `spikes/S2.Decimals`.** It is an xUnit test project that, like `spikes/S1.Encryption`, is not in `MordWare.slnx` and is referenced by nothing. Its tests prove or disprove each point below.
- **Decisions the spike validates** (made in explore, recorded in design.md):
  - **Storage:** every decimal quantity is a fixed-scale signed 64-bit INTEGER column, never TEXT or REAL.
  - **Scale table**, covering every precision mordWare needs:
    - money, including prices per unit: x10^4
    - odometer and km intervals: x100
    - liters and pantry quantities: x1000
    - kWh, Smc and meter readings: x1000
    - nutrition per 100 g: x100
    - rate percent: x10^4
    - FX rate: x10^8
  - **Money:** `Money(amount, currency)`. The stored scale is always x10^4; displayed amounts round to the currency's own exponent.
  - **No silent rounding in storage:** a value that does not fit its scale throws. Calculations round explicitly in domain code.
  - **Stats in memory:** loaded rows are aggregated in C#, not in SQL.
- **The open question the spike decides:** how a domain value type is queried. The candidates (design D4) are:
  - direct mapping with converters
  - raw `long` entity columns mapped to domain types
  - EF complex types
- **Recorded outcome:**
  - pass or fail per criterion, plus measured numbers, in the task notes
  - `docs/roadmap.md`: [S2] marked done with its notes rewritten; Foundation and morVehicles notes get the decided pattern
  - `docs/architecture.md`: the `spikes/` description names S2

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. A spike changes no behavior of the app, so this change sets `skip_specs: true`. How amounts and quantities behave for the user gets its specs in Foundation and morVehicles, where they become real.

## Scope

**In:**
- The `spikes/S2.Decimals` test project. It reuses the spike package versions already in `spikes/Directory.Packages.props` and adds no new package.
- Evidence of what EF Core 10 does with `decimal` (TEXT) columns in SQL ordering and comparison on SQLite.
- Spike-local value types and converters for the scale table, with exact round-trip, extreme, negative and too-many-decimals tests.
- The query-shape decision: range filter, ordering, equality and a unique index on a scaled column. The SQL is inspected and `EXPLAIN QUERY PLAN` must show the index is used.
- `Money` as amount + currency, and how it maps (two columns).
- In-memory stats over 10,000 synthetic refuels, timed.
- Everything on the encrypted SQLCipher v4 connection proven in S1.
- Docs: `docs/roadmap.md` and `docs/architecture.md`.

**Out:**
- **Left to Foundation and the modules:**
  - Any production code in `src/` or `tests/`.
  - Any change to `MordWare.slnx`, the app's central package versions or the architecture tests.
  - Real migrations.
  - Localized input parsing and display formatting of amounts. Guardrail 3 governs those in the modules.
- **Left to morFinance:**
  - The rounding mode for calculations (half-even vs. half-away-from-zero).
  - Whether money amounts are signed or stored as magnitudes.
  - The currency catalog.
- **Not planned:** SQL-side aggregation of amounts.

## Guardrails touched

- **1 (OSS only):** no new dependency. EF Core / Microsoft.Data.Sqlite and SQLite3MC.PCLRaw.bundle are MIT, already vetted in S1.
- **5 (additive, data-safe schema):** the chosen storage must take imported values with 2 or 3 decimals exactly. Scaling them up is lossless. No real schema is created here.
- **6 (no personal data in logs):** the spike uses synthetic data only.
- **7 (DEV and PROD never mix):** every spike database lives in a temporary folder that is deleted afterwards. Nothing touches either edition's LocalState or uses `IAppPaths`.
- **9 (performance):** the index must be used for the real query shapes, and in-memory stats are timed.

## User-visible

No. This is an internal spike, so there is no CHANGELOG entry.

## Roadmap

Item **[S2] Spike: decimal storage on SQLite**. While applying, its status moves to `in progress` and then `done` with this change's name.

## Impact

- **New, isolated content:** `spikes/S2.Decimals`. The app, `src/`, `tests/` and CI are unaffected.
- **Dependencies:** none new. It reuses `Microsoft.EntityFrameworkCore.Sqlite.Core` and `SQLite3MC.PCLRaw.bundle` from `spikes/Directory.Packages.props`.
- **Changed docs:**
  - `docs/roadmap.md`: the [S2], [1] Foundation and [2] morVehicles notes
  - `docs/architecture.md`: the `spikes/` paragraph
