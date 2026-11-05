# Design

## Context

- **Persistence is still empty.** `MordWare.Persistence` is an empty skeleton, and no module has a `Data` layer yet. Foundation will create the first one from what this spike proves.
- **[S1] left a working spike setup.**
  - `spikes/Directory.Packages.props` already pins `Microsoft.EntityFrameworkCore.Sqlite.Core` 10.0.14 and `SQLite3MC.PCLRaw.bundle` 2.6.1, both MIT.
  - `spikes/S1.Encryption` shows the project shape and the SQLCipher v4 connection (URI `file:<encoded path>?cipher=sqlcipher&legacy=4` plus a raw key).
- **Architecture rules that shape the answer** (`docs/architecture.md`, module rules):
  - A module's `Domain` is pure C# with no EF Core.
  - `Data` holds the module's DbContext and mappings.
  - Shared types go in `MordWare.Core`, and shared persistence infrastructure in `MordWare.Persistence`.
- **Precisions to cover** (decimals per quantity):
  - money: 2, in every currency
  - odometer: 2
  - fuel and pantry quantities: 3
  - kWh, Smc and meter readings: 3
  - nutrition: 2
  - rate percentages: 4
  - FX rates: 8

See proposal.md for why the spike exists and what it must prove.

## Goals / Non-Goals

**Goals:**
- Each success criterion becomes a test that passes or fails without anyone reading code. A failure is a valid spike result and gets recorded.
- Foundation and morVehicles can adopt the value types, the converter pattern and the query shape without making a new decision.

**Non-Goals:**
- Production-quality types. The spike's value types are minimal: construction, conversion and comparison only, with no arithmetic beyond what the stats need.
- Localized parsing or formatting of amounts.
- A currency catalog: the spike uses a small hard-coded exponent table (EUR 2, JPY 0, KWD 3).

## Decisions

### D1. Same isolated spike shape as S1

```
spikes/
  Directory.Packages.props      unchanged: reuses the EF Core + SQLite3MC versions
  S1.Encryption/                untouched
  S2.Decimals/
    S2.Decimals.csproj          net10.0, xUnit v3, NOT in MordWare.slnx, not MordWare.*
    SpikeDb.cs                  own copy of the encrypted-connection helper and temp folder
    Quantities.cs               spike value types (D3)
    ...tests per D2-D8
```

- **Own copies, not references.** S2 copies the small `SpikeDb` and temp-folder helpers instead of referencing S1. Foundation deletes each spike independently, so neither may depend on the other.
- **Encrypted connection.** Every database is opened through the S1 connection: SQLCipher v4 URI and raw key. The query and index behavior proven here is then the behavior Foundation actually gets.
- **Alternative rejected:** running on plain SQLite. Encryption sits below the SQL layer, so it should change nothing. Proving that costs one helper, so the spike proves it.

### D2. Scaled INTEGER, with the rejected TEXT behavior recorded as evidence

```
 option              exact?  SQL order/compare        index used?   uniqueness
 ------------------  ------  -----------------------  ------------  -----------------
 decimal as TEXT     yes     wrong, or untranslated   no            textual, not numeric
 REAL (double)       no      correct                  yes           flaky (0.1 + 0.2)
 INTEGER x 10^s      yes     correct                  yes           numeric, exact
```

- **Chosen:** a signed 64-bit INTEGER holding the value times 10^scale. It is exact, SQLite orders and compares it natively, a plain index serves range filters, and summing it is exact.
- **Evidence test.** One test maps a plain `decimal` property on purpose and records what EF Core 10 does with `OrderBy`, `>` and a unique index on it:
  - the exception, or the generated SQL (including any `ef_compare`-style function wrapped around the column)
  - whether a unique index treats `10.5` and `10.50` as one value
  - The result goes into the task notes as the reason TEXT is rejected.
- **Alternatives rejected:**
  - REAL: inexact.
  - Two columns (mantissa + scale): exact, but no simple ordering or indexing.

### D3. One value type per quantity, each with a fixed scale

| Type (spike) | Scale | Range check | Production owner (for Foundation) |
|---|---|---|---|
| `Money(decimal Amount, string Currency)` | x10^4 | fits in Int64 | `MordWare.Core`: shared by vehicles, houses and finance |
| `Km` | x100 | >= 0 | morVehicles `Domain` |
| `Liters` | x1000 | >= 0 | morVehicles `Domain` |
| `Kwh` / `Smc` / `MeterReading` | x1000 | >= 0 | morHouses `Domain` (proved via `Kwh` only) |
| `Quantity` (pantry) | x1000 | >= 0 | morPantry `Domain` (same shape as `Liters`, not built) |
| `Per100g` (nutrition) | x100 | >= 0 | morPantry `Domain` (not built) |
| `RatePercent` | x10^4 | any sign | morFinance `Domain` (proved, as the second x10^4 type) |
| `FxRate` | x10^8 | > 0 | morFinance `Domain` (proved, to stress the widest scale) |

- **Shape:**
  - A `readonly record struct` holding the scaled `long`.
  - Exact conversion to `decimal`.
  - A factory from `decimal` that **throws** when the value has more decimals than the scale or does not fit in Int64. No silent rounding (proposal, What Changes).
  - Comparison operators, plus `IComparable<T>`.
- **Money:**
  - The amount is always stored x10^4.
  - The currency exponent limits what may be *entered* (2 decimals for EUR). That is validation, not storage, so a computed unit price can keep 4 decimals.
  - Rounding to the currency's exponent for display is an explicit method that takes a `MidpointRounding`. The mode is morFinance's decision (Open Questions).
- **Production ownership.**
  - The types stay pure C# in `Domain`, with no EF (architecture rule 3).
  - Converters live in `MordWare.Persistence`. A generic scaled converter is the expected shape; the spike confirms whether that works.
  - A module only uses converters; no module reaches into another module.
- **Rejected:** one universal scale (say x10^8 for everything). It is simpler to convert, but loses the quantity's meaning in the type system, wastes range on money, and makes a raw database value meaningless when read in a tool.

### D4. Query shape: try direct mapping first, fall back in a fixed order

This is the decision the spike exists to make. Each query shape is tested for:
1. **Correct results:** a range filter, `OrderBy`, equality, and `Min`/`Max` over a known synthetic set.
2. **SQL text:** `ToQueryString()` shows the bare column compared to a parameter (`"OdometerKm" > @p`), with no function wrapped around the column.
3. **Index use:** `EXPLAIN QUERY PLAN` on the same SQL reports `USING INDEX` (or `USING COVERING INDEX`) for the range filter.

Candidates, in order of preference:

- **A. Direct mapping with a value converter.** The entity property is `Km`, and a converter maps it to `long`. The range filter is tried in several C# forms, and every form that translates is recorded:
  - the custom operator `r.Odometer > from`
  - `r.Odometer.CompareTo(from) > 0`
  - comparing the converted value through an explicit cast
  - If at least one natural form translates with the index used, A wins. Entities can then be domain types, with no mapping layer.
- **B. Raw `long` columns.** The entity in `Data` has `long OdometerKmX100`, and `Data` maps it to and from `Km`. This always translates, but every module carries a mapping layer, and queries are written in scaled numbers.
- **C. EF complex type.** For multi-column values (`Money`), EF Core's complex-property mapping with its member converter. It is tested for `Money` in any case (D5). For a single-column quantity it is only tried if A fails.

**Outcome rule.** The first candidate meeting all three checks for a single-column quantity is the pattern. Money's mapping is decided separately (D5). If only B works, that is recorded as the Foundation pattern; it is not a failure.

### D5. Money maps to two columns

- **Columns:** amount INTEGER x10^4, plus currency TEXT(3).
- **Mapping:** tried first as a complex property, falling back to two scalar properties if that fails.
- **Tests:**
  - round trip, including a negative amount and a JPY and a KWD value
  - a range filter on the amount within one currency that uses an index on `(Currency, Amount)`
  - ordering by amount within one currency
- **Ordering rule:** comparing amounts across currencies is not meaningful. The value type's comparison throws on mixed currencies, and the spike asserts that.

### D6. Uniqueness on a scaled column

- A refuel table gets a unique index on `(VehicleId, OdometerKm)`, using the same columns as the range filter in D4.
- **Tests:**
  - Two refuels at `12345.10` and `12345.1` km collide (numeric equality).
  - `12345.11` is accepted.
  - The violation surfaces as `DbUpdateException` over `SqliteException` 19 (constraint). The exact types go into the notes for Foundation's error handling.

### D7. Stats in memory, timed

- **Data:** 10,000 synthetic refuels for one vehicle, with increasing odometer, liters, amount in EUR and full-tank flags.
- **Measurement:** load the rows (projected to value types) from the encrypted database, then compute in C#:
  - total liters and total spent
  - average consumption (l/100 km between full tanks)
  - average price per liter as `Money` at x10^4
  - min and max price per liter
- **Recorded numbers:** median load + compute over 10 runs, in Release.
- **Guideline:** under 100 ms for load + compute. Anything above is flagged for an owner decision rather than optimized in the spike.
- **Correctness check:** the stats equal a reference computed with plain `decimal` on the same synthetic data, exactly.

### D8. Edge values

These are table-driven tests for every spike type:
- zero
- the smallest unit (`0.01` km, `0.001` l, `0.0001` money, `0.00000001` FX)
- a large realistic value (999,999.99 km, 1,000,000,000.00 EUR)
- `long.MaxValue` / `MinValue` boundaries: these must throw just past them
- negative values: allowed for `Money` and `RatePercent`, rejected for the others
- one decimal more than the scale: must throw
- imported values: every value with 2 or 3 decimals in the expected range converts exactly

## Risks / Trade-offs

- **[EF does not translate custom operators on converted types]** -> Candidate B is the planned fallback (D4). It costs a mapping layer per module, which Foundation then designs on purpose. The spike records which forms failed, so nobody retries them.
- **[Complex types with value-type structs have EF limitations]** -> D5 falls back to two scalar properties. Money is the only multi-column value, so the cost is contained.
- **[Raw values are hard to read in DB Browser]** -> Accepted: the database is an internal store. The column name carries the scale (`...X100`, `...X10000`) so a raw look stays understandable.
- **[A wrong scale on a column is a 10x-1000x data error]** -> Each type owns its scale constant, the converter takes it from the type, and the edge tests catch a mismatch. Foundation keeps one converter per type and never a hand-written scale.
- **[Overflow at x10^8]** -> FX rates are below 10^6, so x10^8 leaves ample room in Int64. D8 asserts the boundary throws.

## Migration Plan

None: the spike creates no real schema. For imported values with 2 or 3 decimals ([3]), multiplying by their scale is exact; D8 asserts it.

## Open Questions

- **Rounding mode for calculations** (half-even vs. half-away-from-zero). This is morFinance's decision; the spike only proves rounding is always explicit.
- **How many decimals a price per liter shows** (for example 3 for fuel). This is a morVehicles display decision; storage at x10^4 supports any choice up to 4.
