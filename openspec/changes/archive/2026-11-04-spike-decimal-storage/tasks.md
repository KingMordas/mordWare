# Tasks

## 1. Spike scaffolding

- [x] 1.1 Mark roadmap item [S2] `in progress` with change `spike-decimal-storage` in `docs/roadmap.md`. Verify the table and the [S2] section both show it.
- [x] 1.2 Create `spikes/S2.Decimals/S2.Decimals.csproj` (net10.0, xUnit v3, not added to `MordWare.slnx`). It references `Microsoft.EntityFrameworkCore.Sqlite.Core` and `SQLite3MC.PCLRaw.bundle` from the existing `spikes/Directory.Packages.props`, and adds no new package (design D1).
  - Add its own copies of the encrypted-connection helper (SQLCipher v4 URI + raw key) and of the temp-folder helper.
  - Verify `dotnet test --project spikes/S2.Decimals` runs one test green. That test opens an encrypted temp database and reads `sqlite3mc_version()`.
  - Verify `git diff --stat` shows `spikes/Directory.Packages.props` and the root `Directory.Packages.props` unchanged, and that `dotnet build MordWare.slnx` is unaffected.
- [x] 1.3 Update the `spikes/` paragraph of `docs/architecture.md` so it names `S2.Decimals` next to `S1.Encryption`. Verify the paragraph matches the files created in 1.2.

## 2. Evidence against decimal as TEXT

- [x] 2.1 Add the evidence test (design D2): an entity with a plain `decimal` property on an encrypted database. Record for EF Core 10 in the task notes:
  - what happens with `OrderBy`, `>` and `Min`/`Max` on it (the exception, or `ToQueryString()` and the results)
  - whether a unique index accepts both `10.5` and `10.50`
  - Verify with `dotnet test --project spikes/S2.Decimals --filter-class "*TextDecimal*"`. The test asserts what was observed, so the evidence stays reproducible.
  - Result (2026-11-04): PASS (5 tests). EF Core 10 throws nothing. It stores `decimal` as TEXT (`9.0`, `100.5`), and LINQ results are correct (`OrderBy`, `>`, `Min`, `Max`, `Sum`), but only because EF rewrites the SQL:
    - `ORDER BY ... COLLATE EF_DECIMAL`
    - `WHERE ef_compare("r"."Value", '9.5') > 0`
    - Both helpers exist only on EF's own connection.
    - **Range filters cannot use the index:** `EXPLAIN QUERY PLAN` reports `SCAN`, because the column is wrapped in `ef_compare`.
    - **Plain SQL sorts as text:** `-2.0, 10.0, 100.5, 9.0`. That is what DB Browser, a raw query or an import script would see.
    - **Uniqueness holds only by convention:** EF normalizes `100.50m` to `100.5`, so the duplicate collides (`DbUpdateException` over `SqliteException` 19). A raw `INSERT ... '100.50'` is accepted next to `100.5`.
    - **Conclusion:** TEXT is rejected for performance (no index use, guardrail 9) and for robustness (correctness depends on EF-only helpers and on EF's text normalization). The explore assumed TEXT gave wrong results; on EF Core 10 it gives slow and fragile ones instead.

## 3. Value types and exact conversion

- [x] 3.1 Add the spike value types of design D3:
  - `Money`, `Km`, `Liters`, `Kwh`, `RatePercent` and `FxRate`, each a `readonly record struct` over a scaled `long`, with its scale constant
  - a factory from `decimal` that throws when the value is not exact or out of range
  - exact conversion back to `decimal`
  - comparison operators and `IComparable<T>`
  - `Money` comparison throws on mixed currencies
  - Verify with `--filter-class "*Quantity*"` (construction, conversion and comparison tests).
  - Result (2026-11-04): PASS (8 tests). Each type implements `IScaled<TSelf>` with `static abstract int Decimals` and `static abstract TSelf FromScaled(long)`, so a single generic `ScaledConverter<T>` takes the scale from the type itself.
    - Gotcha for Foundation: an expression tree cannot call a static abstract member (CS8927). The converter calls it through an ordinary static helper method.
    - `Money` equality across currencies is simply false. Comparison and `+` throw `InvalidOperationException`. A malformed currency code throws `ArgumentException`.
- [x] 3.2 Add the table-driven edge tests of design D8 for every spike type:
  - zero and the smallest unit
  - large realistic values
  - throws just past the Int64 boundary
  - negatives allowed only for `Money` and `RatePercent`
  - one decimal too many throws
  - imported values with 2 or 3 decimals convert exactly
  - Verify with `--filter-class "*Edge*"`.
  - Result (2026-11-04): PASS (41 test cases).
    - Every boundary `long.MaxValue / 10^s` (and `MinValue` for money) converts exactly, and one unit past it throws `ArgumentOutOfRangeException`. So does a value whose scaling overflows `decimal`.
    - `Money` `+` overflow throws `OverflowException` (checked arithmetic).
    - One decimal too many throws `ArgumentException`, never a rounded value.
    - **Importer finding:** every value with 2 decimals fits `Km` (x100), and every value with 3 decimals fits `Liters` / `Kwh` (x1000), over a 16-digit range. `Money` at x10^4 holds +/-922 trillion. That covers every realistic amount, but not the extremes of a 16-digit range (up to 10^16); those are refused, never truncated. The importer ([3]) must treat such a value as an error.
- [x] 3.3 Add the `Money` display-rounding test: rounding to the currency exponent (EUR 2, JPY 0, KWD 3) is explicit and takes a `MidpointRounding`, and both modes give the expected results on a `.xx5` value.
  - Verify with `--filter-class "*MoneyRounding*"`.
  - Result (2026-11-04): PASS (8 test cases). `RoundForDisplay(MidpointRounding)` rounds to EUR 2, JPY 0 and KWD 3 decimals, and the two modes differ as expected on `2.125`, `-2.125`, `2.5` and `1.2345`. Storage keeps 4 decimals in every currency. A price per unit (`Money.Per(quantity, mode)`) rounds explicitly to storage scale: 50.00 EUR / 26.89 l = 1.8594 EUR/l.

## 4. Query shape (the decision)

- [x] 4.1 Add a refuel entity mapped with candidate A of design D4 (`Km` property + value converter to `long`), with an index on `(VehicleId, OdometerKm)`, on an encrypted database. For each C# form of the range filter (custom operator, `CompareTo`, explicit cast) assert:
  - correct results on a known set
  - `ToQueryString()` compares the bare column to a parameter
  - `EXPLAIN QUERY PLAN` reports the index
  - Also assert `OrderBy`, equality and `Min`/`Max`. Record in the task notes which forms translate and which throw (with the message).
  - Verify with `--filter-class "*DirectMapping*"`.
  - Result (2026-11-04): PASS (6 tests). The column is a plain `INTEGER` holding `km x 100` (`12345.10` -> `1234510`).
    - **Translate to the bare column, with the index used:**
      - the custom operators `>`, `>=`, `<`, `<=` and `==` on `Km`, e.g. `"r"."OdometerKm" > @from`, plan `SEARCH r USING COVERING INDEX IX_Refuels_VehicleId_OdometerKm (VehicleId=? AND OdometerKm>? AND OdometerKm<?)` for a two-sided range
      - `CompareTo(x) > 0`, which gives the same SQL and plan
      - `OrderBy`: `ORDER BY "r"."OdometerKm"` read in index order, with no `TEMP B-TREE` sort step
      - `Min` / `Max`
      - EF converts the parameter through the same converter, so results are correct on the known set (with noise rows on another vehicle).
    - **Do not work:**
      - `r.OdometerKm.Scaled > n` throws `InvalidOperationException` ("The LINQ expression ... could not be translated"), because `Scaled` is not mapped.
      - `EF.Property<long>(r, "OdometerKm") > n` throws `InvalidCastException` ("Invalid cast from 'System.Int64' to 'S2.Decimals.Km'").
      - `(long)(object)r.OdometerKm > n` translates, but to `CAST("r"."OdometerKm" AS INTEGER) > @p`, so the range part loses the index.
- [x] 4.2 **Only if no form in 4.1 meets all three checks:** add candidate B (raw `long` column + mapping in the data layer) and candidate C (complex property), and run the same assertions.
  - Verify with `--filter-class "*Fallback*"`.
  - If 4.1 already passes, mark this task done with the note "not needed" and the reason.
  - Result (2026-11-04): not needed. Candidate A meets all three checks in 4.1 (correct results, bare column against a parameter, index in the plan), so no `*Fallback*` tests exist. Complex-property mapping is still exercised for `Money` in 5.1, as design D5 requires.
- [x] 4.3 Record the query-shape outcome in the task notes, per the outcome rule in design D4: the pattern Foundation adopts, and why.
  - Verify the note names the winning candidate and the failed forms.
  - Result (2026-11-04): **Candidate A wins: direct mapping.**
    - Entities use the domain value types themselves, mapped with one generic `ScaledConverter<T>` per property. There is no separate data-layer entity and no mapping code.
    - Queries are written in domain terms (`r.OdometerKm > from`), with the operators or `CompareTo`.
    - **Never** reach for `.Scaled`, `EF.Property<long>` or a cast in a query: the first two throw and the cast loses the index.
    - Candidates B (raw `long` + mapping) and C (complex type for a single quantity) are not needed.

## 5. Money mapping and uniqueness

- [x] 5.1 Add the `Money` mapping tests (design D5): amount x10^4 + currency in two columns, tried first as a complex property, then as two scalar properties. Assert:
  - round trip for EUR (negative included), JPY and KWD
  - a range filter on amount within one currency, with `EXPLAIN QUERY PLAN` showing the `(Currency, Amount)` index
  - ordering within one currency
  - Record which mapping worked.
  - Verify with `--filter-class "*MoneyMapping*"`.
  - Result (2026-11-04): PASS (6 tests). **Two scalar properties win.**
    - **Complex property (attempt 1):**
      - It maps `Money` to `SpentAmount INTEGER` + `SpentCurrency TEXT` and round-trips EUR (incl. -12.5 and 1.8594), JPY and KWD.
      - EF Core 10 **cannot declare an index** on complex-type members: `HasIndex(r => new { r.Spent.Currency, r.Spent.ScaledAmount })` throws `ArgumentException` ("is not a valid member access expression"). Every query is therefore `SCAN r`.
      - `Money`'s own `>` does not translate on a complex type (`InvalidOperationException`). Only member comparisons in scaled numbers do.
    - **Two scalar properties (attempt 2, chosen):**
      - The entity has `SpentAmount` (`MoneyAmount`, through the same `ScaledConverter<T>`) and `SpentCurrency` (TEXT(3)), plus an unmapped `Money Spent` view of the pair.
      - It round-trips the same samples (raw column `-125000` for -12.5 EUR).
      - `SpentCurrency == min.Currency && SpentAmount > min.Value` uses `SEARCH ... USING COVERING INDEX IX_Refuels_SpentCurrency_SpentAmount (SpentCurrency=? AND SpentAmount>?)`.
      - Ordering within a currency reads in index order, with no `TEMP B-TREE`.
    - **Added during apply:** the small `MoneyAmount` scaled type (x10^4, any sign) for the amount column, so `Money` columns follow the same converter-and-operators pattern as every other quantity. It lives next to `Money` (`MordWare.Core` in production).
- [x] 5.2 Add the uniqueness tests (design D6) on the unique index `(VehicleId, OdometerKm)`:
  - `12345.10` and `12345.1` collide
  - `12345.11` is accepted
  - Record the exception types and the SQLite error code in the task notes.
  - Verify with `--filter-class "*Uniqueness*"`.
  - Result (2026-11-04): PASS (3 tests). Both readings are the integer `1234510`, so `12345.1` after `12345.10` on the same vehicle is refused with `DbUpdateException` over `SqliteException`, `SqliteErrorCode` 19 (SQLITE_CONSTRAINT) and `SqliteExtendedErrorCode` 2067 (SQLITE_CONSTRAINT_UNIQUE). Foundation can map 2067 to a localized "already exists" message.
    - Unlike TEXT (2.1), a raw `INSERT ... 1234510` is refused too, so uniqueness no longer depends on how the writer formats the value.
    - `12345.11` on the same vehicle and `12345.10` on another vehicle are both accepted.

## 6. Stats in memory

- [x] 6.1 Add the stats measurement (design D7): seed 10,000 synthetic refuels into an encrypted database, then load them as value types and compute:
  - total liters
  - total spent
  - average consumption between full tanks
  - average, min and max price per liter
  - Assert the results equal a plain-`decimal` reference exactly.
  - Record the median load + compute time over 10 runs (Release) in the task notes, and flag anything above 100 ms for an owner decision.
  - Verify with `dotnet test --project spikes/S2.Decimals -c Release --filter-class "*Stats*"`.
  - Result (2026-11-04, Release build, this PC, two runs): PASS.
    - **Exact:** the domain path (value types loaded from the encrypted database, then `Liters` / `Money` sums, `Money.Per` with explicit `ToEven` rounding) equals the plain-`decimal` reference exactly. Results: 401,438.084 l, 742,101.70 EUR, 7.2949 l/100 km, average 1.8486 EUR/l, min 1.5999 and max 2.0992 EUR/l.
    - **Timing:** load (projection, `AsNoTracking`, ordered by the indexed odometer) + compute takes a median of 13.4-13.6 ms over 10 runs, after one discarded warm-up run (min 12.8 ms, max 49.5 ms). That is far inside the 100 ms guideline, so there is nothing to flag.
    - **Data:** 10,000 refuels with a fixed random seed (300-800 km apart, 20-60 l, 1.600-2.099 EUR/l, about 80% full tanks).

## 7. Outcome and docs

- [x] 7.1 Rewrite the [S2] section of `docs/roadmap.md` with the outcome and mark [S2] `done` with this change. The outcome covers:
  - why TEXT was rejected (evidence from 2.1)
  - the scale table
  - the value-type shape
  - the query-shape pattern
  - the Money mapping
  - uniqueness behavior
  - the stats timing
  - Verify the outdated "Proposed" notes are replaced by decided ones.
  - Result (2026-11-04): done. The [S2] section now has Status / Question / Outcome. The old "Proposed" and "Success test" notes are gone, and so is the explore-time claim that TEXT "compares and sorts wrongly", replaced by the measured behavior. The table row reads `done`.
- [x] 7.2 Update the [1] Foundation and [2] morVehicles notes in `docs/roadmap.md` with what they inherit:
  - Foundation: the converters in `MordWare.Persistence`, `Money` in `MordWare.Core`, the query pattern, and deleting `spikes/S2.Decimals` once adopted
  - morVehicles: `Km` and `Liters` in its `Domain`
  - both design.md open questions: the rounding mode goes to [6] morFinance, and the price-per-liter decimals to [2]
  - Verify each open question appears in the right item.
  - Result (2026-11-04): done.
    - Item [1] Foundation got a "Decimal storage (from [S2])" block: the shared types in Core, the converter in Persistence, never a raw `decimal`, Money as a column pair, the unique-violation code, and deleting the spike.
    - Item [2] morVehicles got its storage note and the price-per-liter open question.
    - Item [6] morFinance got the rounding-mode open question, plus signed vs. magnitude amounts.
    - Item [3] Importer also got the money-range finding from 3.2, which belongs to it.
- [x] 7.3 Run the whole spike (`dotnet test --project spikes/S2.Decimals`), `dotnet build MordWare.slnx`, the architecture tests (`dotnet test --project tests/MordWare.Tests.Architecture`) and `openspec validate spike-decimal-storage --strict`. Verify all four succeed and that no spike temp folder remains in `%TEMP%`.
  - Result (2026-11-04): PASS.
    - Spike: 79 passed, 0 skipped.
    - `MordWare.slnx` builds with 0 warnings and 0 errors.
    - Architecture tests 5/5.
    - `openspec validate spike-decimal-storage --strict` is valid.
    - No `mordware-s2*` folder remains in `%TEMP%`.

## Workflow follow-up

- Run /opsx:archive. There are no delta specs (`skip_specs`), so archive moves the change, confirms [S2] is `done` in `docs/roadmap.md`, and ends with one Conventional Commit on `main`.
- No CHANGELOG entry: the change is not user-visible.
