using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace S2.Decimals;

/// <summary>
/// Task 4.1 / design D4, candidate A: the entity property is the domain type (Km), mapped by ScaledConverter to a
/// plain INTEGER column. Each query form is checked for (1) correct results, (2) the bare column compared with a
/// parameter and (3) the index in EXPLAIN QUERY PLAN.
/// </summary>
public sealed class DirectMappingTests : IDisposable
{
    public sealed class Refuel
    {
        public int Id { get; set; }
        public int VehicleId { get; set; }
        public Km OdometerKm { get; set; }
    }

    private sealed class Ctx(string cs) : DbContext
    {
        public DbSet<Refuel> Refuels => Set<Refuel>();
        protected override void OnConfiguring(DbContextOptionsBuilder b) => b.UseSqlite(cs);
        protected override void OnModelCreating(ModelBuilder b)
        {
            var refuel = b.Entity<Refuel>();
            refuel.Property(r => r.OdometerKm).HasConversion<ScaledConverter<Km>>();
            refuel.HasIndex(r => new { r.VehicleId, r.OdometerKm });
        }
    }

    private const string Index = "IX_Refuels_VehicleId_OdometerKm";
    private static readonly decimal[] Readings = [9m, 10m, 100.5m, 12345.1m, 12345.11m];

    private readonly TempFolder _temp = new();
    private readonly string _cs;

    public DirectMappingTests()
    {
        _cs = SpikeDb.ConnectionString(_temp.File("direct.db"), SpikeDb.NewRawKey());
        using var db = new Ctx(_cs);
        db.Database.EnsureCreated();
        foreach (var km in Readings)
        {
            db.Refuels.Add(new Refuel { VehicleId = 1, OdometerKm = Km.Of(km) });
            db.Refuels.Add(new Refuel { VehicleId = 2, OdometerKm = Km.Of(km + 1m) }); // noise on another vehicle
        }
        db.SaveChanges();
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Column_is_a_plain_INTEGER()
    {
        using var db = new Ctx(_cs);
        Assert.Contains("\"OdometerKm\" INTEGER NOT NULL", db.Database.GenerateCreateScript());
        Assert.Equal("integer", Connection(db).Scalar("SELECT DISTINCT typeof(OdometerKm) FROM Refuels"));
        Assert.Equal(1234510L, Connection(db).Scalar("SELECT OdometerKm FROM Refuels WHERE VehicleId = 1 ORDER BY OdometerKm DESC LIMIT 1 OFFSET 1"));
    }

    [Fact]
    public void Custom_operators_translate_to_an_indexed_range()
    {
        var from = Km.Of(9.5m);
        var to = Km.Of(12345.1m);
        using var db = new Ctx(_cs);

        var query = db.Refuels.Where(r => r.VehicleId == 1 && r.OdometerKm > from && r.OdometerKm <= to);

        Assert.Equal([10m, 100.5m, 12345.1m], Values(query));
        var sql = query.ToQueryString();
        Assert.Contains("\"r\".\"OdometerKm\" > @from", sql);
        Assert.Contains("\"r\".\"OdometerKm\" <= @to", sql);
        Assert.Contains($"USING COVERING INDEX {Index} (VehicleId=? AND OdometerKm>? AND OdometerKm<?)", Plan(db, sql));
    }

    [Fact]
    public void Every_comparison_operator_translates_to_the_bare_column()
    {
        var at = Km.Of(100.5m);
        using var db = new Ctx(_cs);

        Assert.Equal([100.5m, 12345.1m, 12345.11m], Values(db.Refuels.Where(r => r.VehicleId == 1 && r.OdometerKm >= at)));
        Assert.Equal([9m, 10m], Values(db.Refuels.Where(r => r.VehicleId == 1 && r.OdometerKm < at)));
        Assert.Equal([9m, 10m, 100.5m], Values(db.Refuels.Where(r => r.VehicleId == 1 && r.OdometerKm <= at)));
        Assert.Equal([100.5m], Values(db.Refuels.Where(r => r.VehicleId == 1 && r.OdometerKm == at)));

        var sql = db.Refuels.Where(r => r.VehicleId == 1 && r.OdometerKm == at).ToQueryString();
        Assert.Contains("\"r\".\"OdometerKm\" = @at", sql);
        Assert.Contains($"(VehicleId=? AND OdometerKm=?)", Plan(db, sql));
    }

    [Fact]
    public void CompareTo_translates_like_the_operator()
    {
        var from = Km.Of(9.5m);
        using var db = new Ctx(_cs);

        var query = db.Refuels.Where(r => r.VehicleId == 1 && r.OdometerKm.CompareTo(from) > 0);

        Assert.Equal([10m, 100.5m, 12345.1m, 12345.11m], Values(query));
        var sql = query.ToQueryString();
        Assert.Contains("\"r\".\"OdometerKm\" > @from", sql);
        Assert.Contains("(VehicleId=? AND OdometerKm>?)", Plan(db, sql));
    }

    [Fact]
    public void OrderBy_reads_in_index_order_and_Min_Max_work()
    {
        using var db = new Ctx(_cs);

        var query = db.Refuels.Where(r => r.VehicleId == 1).OrderBy(r => r.OdometerKm);

        Assert.Equal(Readings, query.AsEnumerable().Select(r => r.OdometerKm.Value).ToArray());
        var sql = query.ToQueryString();
        Assert.EndsWith("ORDER BY \"r\".\"OdometerKm\"", sql.TrimEnd());
        var plan = Plan(db, sql);
        Assert.Contains($"USING COVERING INDEX {Index} (VehicleId=?)", plan);
        Assert.DoesNotContain("TEMP B-TREE", plan); // no sort step: the index already orders the rows

        Assert.Equal(Km.Of(12345.11m), db.Refuels.Where(r => r.VehicleId == 1).Max(r => r.OdometerKm));
        Assert.Equal(Km.Of(9m), db.Refuels.Where(r => r.VehicleId == 1).Min(r => r.OdometerKm));
    }

    [Fact]
    public void Forms_that_do_not_work()
    {
        var from = Km.Of(9.5m);
        var fromScaled = from.Scaled;
        using var db = new Ctx(_cs);

        // The unmapped Scaled property cannot be translated.
        Assert.Throws<InvalidOperationException>(() => db.Refuels.Where(r => r.OdometerKm.Scaled > fromScaled).ToList());

        // EF.Property<long> on a converted property: EF casts the long back to the model type and fails.
        Assert.Throws<InvalidCastException>(() =>
            db.Refuels.Where(r => EF.Property<long>(r, nameof(Refuel.OdometerKm)) > fromScaled).ToList());

        // A cast through object translates, but wraps the column in CAST(...), so the range loses the index.
        var cast = db.Refuels.Where(r => r.VehicleId == 1 && (long)(object)r.OdometerKm > fromScaled);
        var sql = cast.ToQueryString();
        Assert.Contains("CAST(\"r\".\"OdometerKm\" AS INTEGER) >", sql);
        Assert.DoesNotContain("OdometerKm>?", Plan(db, sql));
    }

    private static decimal[] Values(IQueryable<Refuel> query) =>
        query.OrderBy(r => r.OdometerKm).AsEnumerable().Select(r => r.OdometerKm.Value).ToArray();

    private static SqliteConnection Connection(Ctx db)
    {
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        connection.Open();
        return connection;
    }

    private static string Plan(Ctx db, string queryString) =>
        Connection(db).QueryPlan(queryString[queryString.IndexOf("SELECT", StringComparison.Ordinal)..]);
}
