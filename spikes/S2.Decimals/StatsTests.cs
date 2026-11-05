using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace S2.Decimals;

/// <summary>
/// Task 6.1 / design D7: stats over 10,000 synthetic refuels, loaded from the encrypted database as value types
/// and computed in C#. They must equal a plain-decimal reference exactly; the time is recorded, not asserted
/// (guideline: under 100 ms for load + compute; anything above is flagged for an owner decision).
/// </summary>
public sealed class StatsTests(ITestOutputHelper output) : IDisposable
{
    public sealed class Refuel
    {
        public int Id { get; set; }
        public int VehicleId { get; set; }
        public DateOnly Date { get; set; }
        public Km OdometerKm { get; set; }
        public Liters Fuel { get; set; }
        public MoneyAmount SpentAmount { get; set; }
        public string SpentCurrency { get; set; } = "";
        public bool IsFullTank { get; set; }
    }

    private sealed class Ctx(string cs) : DbContext
    {
        public DbSet<Refuel> Refuels => Set<Refuel>();
        protected override void OnConfiguring(DbContextOptionsBuilder b) => b.UseSqlite(cs);
        protected override void OnModelCreating(ModelBuilder b)
        {
            var refuel = b.Entity<Refuel>();
            refuel.Property(r => r.OdometerKm).HasConversion<ScaledConverter<Km>>();
            refuel.Property(r => r.Fuel).HasConversion<ScaledConverter<Liters>>();
            refuel.Property(r => r.SpentAmount).HasConversion<ScaledConverter<MoneyAmount>>();
            refuel.Property(r => r.SpentCurrency).HasMaxLength(3);
            refuel.HasIndex(r => new { r.VehicleId, r.OdometerKm }).IsUnique();
        }
    }

    /// <summary>The same refuel as plain decimals, for the reference computation.</summary>
    private sealed record Plain(decimal Km, decimal Liters, decimal Amount, bool Full);

    /// <summary>The stats, as plain decimals so domain and reference results compare exactly.</summary>
    private sealed record Stats(
        decimal TotalLiters, decimal TotalSpent, decimal LitersPer100Km,
        decimal AvgPricePerLiter, decimal MinPricePerLiter, decimal MaxPricePerLiter);

    private const int Count = 10_000;
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Stats_over_10k_refuels_are_exact_and_timed()
    {
        var plain = Synthetic();
        var cs = SpikeDb.ConnectionString(_temp.File("stats.db"), SpikeDb.NewRawKey());
        Seed(cs, plain);

        var reference = ComputeReference(plain);

        var timings = new List<double>();
        Stats? result = null;
        for (var run = 0; run < 11; run++) // the first run warms up EF's model and query cache; it is discarded
        {
            var watch = Stopwatch.StartNew();
            result = LoadAndCompute(cs);
            watch.Stop();
            if (run > 0)
            {
                timings.Add(watch.Elapsed.TotalMilliseconds);
            }
        }

        Assert.Equal(reference, result);

        timings.Sort();
        var median = (timings[4] + timings[5]) / 2;
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        output.WriteLine(FormattableString.Invariant($"{configuration}: load + compute over {Count} refuels, median of 10: {median:F1} ms (min {timings[0]:F1}, max {timings[^1]:F1})"));
        output.WriteLine(FormattableString.Invariant($"stats: {result}"));
    }

    private static List<Plain> Synthetic()
    {
        var random = new Random(20261104); // fixed seed: the same data on every run
        var list = new List<Plain>(Count);
        var km = 15_000.00m;
        for (var i = 0; i < Count; i++)
        {
            km += random.Next(30_000, 80_000) / 100m;            // 300.00 - 799.99 km, 2 decimals
            var liters = random.Next(20_000, 60_000) / 1000m;     // 20.000 - 59.999 l, 3 decimals
            var price = random.Next(1_600, 2_100) / 1000m;        // 1.600 - 2.099 EUR/l
            var amount = Math.Round(liters * price, 2, MidpointRounding.AwayFromZero); // as on a receipt
            list.Add(new Plain(km, liters, amount, Full: random.Next(5) != 0)); // ~80% full tanks
        }
        return list;
    }

    private static void Seed(string cs, List<Plain> plain)
    {
        using var db = new Ctx(cs);
        db.Database.EnsureCreated();
        var day = new DateOnly(2000, 1, 1);
        db.Refuels.AddRange(plain.Select((p, i) => new Refuel
        {
            VehicleId = 1,
            Date = day.AddDays(i),
            OdometerKm = Km.Of(p.Km),
            Fuel = Liters.Of(p.Liters),
            SpentAmount = MoneyAmount.Of(p.Amount),
            SpentCurrency = "EUR",
            IsFullTank = p.Full,
        }));
        db.SaveChanges();
    }

    /// <summary>The domain path: load value types, compute with them.</summary>
    private static Stats LoadAndCompute(string cs)
    {
        using var db = new Ctx(cs);
        var rows = db.Refuels.AsNoTracking()
            .Where(r => r.VehicleId == 1)
            .OrderBy(r => r.OdometerKm)
            .Select(r => new { r.OdometerKm, r.Fuel, r.SpentAmount, r.SpentCurrency, r.IsFullTank })
            .ToList();

        var totalLiters = Liters.Of(0m);
        var totalSpent = Money.Of(0m, "EUR");
        Money? minPrice = null, maxPrice = null;

        // Consumption between full tanks: the fuel bought after the first full tank, up to and including the
        // last one, over the distance between them.
        Km? firstFull = null, lastFull = null;
        var fuelSinceFirstFull = Liters.Of(0m);
        var fuelAtLastFull = Liters.Of(0m);

        foreach (var r in rows)
        {
            var spent = Money.Of(r.SpentAmount, r.SpentCurrency);
            totalLiters += r.Fuel;
            totalSpent += spent;

            var price = spent.Per(r.Fuel.Value, MidpointRounding.ToEven);
            minPrice = minPrice is { } min && min <= price ? min : price;
            maxPrice = maxPrice is { } max && max >= price ? max : price;

            if (firstFull is not null)
            {
                fuelSinceFirstFull += r.Fuel;
            }
            if (r.IsFullTank)
            {
                firstFull ??= r.OdometerKm;
                lastFull = r.OdometerKm;
                fuelAtLastFull = fuelSinceFirstFull;
            }
        }

        var distance = lastFull!.Value.Value - firstFull!.Value.Value;
        return new Stats(
            totalLiters.Value,
            totalSpent.Amount,
            Math.Round(fuelAtLastFull.Value / distance * 100m, 4, MidpointRounding.ToEven),
            totalSpent.Per(totalLiters.Value, MidpointRounding.ToEven).Amount,
            minPrice!.Value.Amount,
            maxPrice!.Value.Amount);
    }

    /// <summary>The reference path: the same formulas on plain decimals, never touching the database.</summary>
    private static Stats ComputeReference(List<Plain> plain)
    {
        var ordered = plain.OrderBy(p => p.Km).ToList();
        var prices = ordered.Select(p => Math.Round(p.Amount / p.Liters, 4, MidpointRounding.ToEven)).ToList();

        var firstFullIndex = ordered.FindIndex(p => p.Full);
        var lastFullIndex = ordered.FindLastIndex(p => p.Full);
        var fuel = ordered.Skip(firstFullIndex + 1).Take(lastFullIndex - firstFullIndex).Sum(p => p.Liters);
        var distance = ordered[lastFullIndex].Km - ordered[firstFullIndex].Km;

        var totalLiters = ordered.Sum(p => p.Liters);
        var totalSpent = ordered.Sum(p => p.Amount);
        return new Stats(
            totalLiters,
            totalSpent,
            Math.Round(fuel / distance * 100m, 4, MidpointRounding.ToEven),
            Math.Round(totalSpent / totalLiters, 4, MidpointRounding.ToEven),
            prices.Min(),
            prices.Max());
    }
}
