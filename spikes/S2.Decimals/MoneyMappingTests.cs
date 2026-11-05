using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace S2.Decimals;

/// <summary>
/// Task 5.1 / design D5: Money as amount (x10^4) + currency in two columns. Tried first as an EF complex property,
/// then as two scalar properties. The complex property round-trips but cannot carry an index; the scalar pair
/// can, and queries on it stay in domain terms.
/// </summary>
public sealed class MoneyMappingTests : IDisposable
{
    private static readonly Money[] Samples =
    [
        Money.Of(-12.5m, "EUR"), Money.Of(50m, "EUR"), Money.Of(1.8594m, "EUR"), Money.Of(500m, "JPY"), Money.Of(1.234m, "KWD"),
    ];

    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    // ---- Attempt 1: complex property -------------------------------------------------------------------------

    public sealed class ComplexRefuel
    {
        public int Id { get; set; }
        public Money Spent { get; set; }
    }

    private class ComplexCtx(string cs) : DbContext
    {
        public DbSet<ComplexRefuel> Refuels => Set<ComplexRefuel>();
        protected override void OnConfiguring(DbContextOptionsBuilder b) => b.UseSqlite(cs);
        protected override void OnModelCreating(ModelBuilder b)
        {
            var refuel = b.Entity<ComplexRefuel>();
            refuel.ComplexProperty(r => r.Spent, m =>
            {
                m.Property(x => x.ScaledAmount).HasColumnName("SpentAmount");
                m.Property(x => x.Currency).HasColumnName("SpentCurrency").HasMaxLength(3);
            });
            Configure(refuel);
        }

        protected virtual void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ComplexRefuel> refuel)
        {
        }
    }

    /// <summary>Its own type, because EF caches the model per context type.</summary>
    private sealed class ComplexIndexCtx(string cs) : ComplexCtx(cs)
    {
        protected override void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ComplexRefuel> refuel) =>
            refuel.HasIndex(r => new { r.Spent.Currency, r.Spent.ScaledAmount });
    }

    private string SeedComplex()
    {
        var cs = SpikeDb.ConnectionString(_temp.File("complex.db"), SpikeDb.NewRawKey());
        using var db = new ComplexCtx(cs);
        db.Database.EnsureCreated();
        db.Refuels.AddRange(Samples.Select(m => new ComplexRefuel { Spent = m }));
        db.SaveChanges();
        return cs;
    }

    [Fact]
    public void Complex_property_round_trips_into_two_columns()
    {
        var cs = SeedComplex();
        using var db = new ComplexCtx(cs);

        var script = db.Database.GenerateCreateScript();
        Assert.Contains("\"SpentAmount\" INTEGER NOT NULL", script);
        Assert.Contains("\"SpentCurrency\" TEXT NOT NULL", script);
        Assert.Equal(Samples, db.Refuels.OrderBy(r => r.Id).AsEnumerable().Select(r => r.Spent).ToArray());
    }

    [Fact]
    public void Complex_property_cannot_declare_an_index()
    {
        var cs = SpikeDb.ConnectionString(_temp.File("complex-index.db"), SpikeDb.NewRawKey());
        using var db = new ComplexIndexCtx(cs);

        var ex = Assert.Throws<ArgumentException>(() => db.Model);
        Assert.Contains("is not a valid member access expression", ex.Message);
    }

    [Fact]
    public void Complex_property_queries_translate_only_through_members_and_scan()
    {
        var cs = SeedComplex();
        var min = Money.Of(0m, "EUR");
        var minScaled = min.ScaledAmount;
        using var db = new ComplexCtx(cs);

        // Money's own operator does not translate on a complex type.
        Assert.Throws<InvalidOperationException>(() => db.Refuels.Where(r => r.Spent > min).ToList());

        // Its members do, but in scaled numbers, and with no index to use.
        var query = db.Refuels.Where(r => r.Spent.Currency == "EUR" && r.Spent.ScaledAmount > minScaled);
        Assert.Equal([1.8594m, 50m], query.AsEnumerable().Select(r => r.Spent.Amount).Order().ToArray());
        Assert.Equal("SCAN r", Plan(db, query.ToQueryString()));
    }

    // ---- Attempt 2: two scalar properties ---------------------------------------------------------------------

    public sealed class ScalarRefuel
    {
        public int Id { get; set; }
        public MoneyAmount SpentAmount { get; set; }
        public string SpentCurrency { get; set; } = "";

        /// <summary>The domain view of the pair; not mapped.</summary>
        public Money Spent
        {
            get => Money.Of(SpentAmount, SpentCurrency);
            set => (SpentAmount, SpentCurrency) = (value.Value, value.Currency);
        }
    }

    private sealed class ScalarCtx(string cs) : DbContext
    {
        public DbSet<ScalarRefuel> Refuels => Set<ScalarRefuel>();
        protected override void OnConfiguring(DbContextOptionsBuilder b) => b.UseSqlite(cs);
        protected override void OnModelCreating(ModelBuilder b)
        {
            var refuel = b.Entity<ScalarRefuel>();
            refuel.Ignore(r => r.Spent);
            refuel.Property(r => r.SpentAmount).HasConversion<ScaledConverter<MoneyAmount>>();
            refuel.Property(r => r.SpentCurrency).HasMaxLength(3);
            refuel.HasIndex(r => new { r.SpentCurrency, r.SpentAmount });
        }
    }

    private string SeedScalar()
    {
        var cs = SpikeDb.ConnectionString(_temp.File("scalar.db"), SpikeDb.NewRawKey());
        using var db = new ScalarCtx(cs);
        db.Database.EnsureCreated();
        db.Refuels.AddRange(Samples.Select(m => new ScalarRefuel { Spent = m }));
        db.SaveChanges();
        return cs;
    }

    [Fact]
    public void Scalar_pair_round_trips_into_two_columns()
    {
        var cs = SeedScalar();
        using var db = new ScalarCtx(cs);

        var script = db.Database.GenerateCreateScript();
        Assert.Contains("\"SpentAmount\" INTEGER NOT NULL", script);
        Assert.Contains("\"SpentCurrency\" TEXT NOT NULL", script);
        Assert.Contains("CREATE INDEX \"IX_Refuels_SpentCurrency_SpentAmount\"", script);
        Assert.Equal(Samples, db.Refuels.OrderBy(r => r.Id).AsEnumerable().Select(r => r.Spent).ToArray());
        Assert.Equal(-125_000L, Connection(db).Scalar("SELECT SpentAmount FROM Refuels WHERE Id = 1"));
    }

    [Fact]
    public void Scalar_pair_range_within_a_currency_uses_the_index()
    {
        var cs = SeedScalar();
        var min = Money.Of(0m, "EUR");
        using var db = new ScalarCtx(cs);

        var query = db.Refuels.Where(r => r.SpentCurrency == min.Currency && r.SpentAmount > min.Value);

        Assert.Equal([1.8594m, 50m], query.AsEnumerable().Select(r => r.Spent.Amount).Order().ToArray());
        var sql = query.ToQueryString();
        Assert.Contains("\"r\".\"SpentAmount\" > @", sql);
        Assert.Contains("USING COVERING INDEX IX_Refuels_SpentCurrency_SpentAmount (SpentCurrency=? AND SpentAmount>?)", Plan(db, sql));
    }

    [Fact]
    public void Scalar_pair_orders_within_a_currency_in_index_order()
    {
        var cs = SeedScalar();
        using var db = new ScalarCtx(cs);

        var query = db.Refuels.Where(r => r.SpentCurrency == "EUR").OrderBy(r => r.SpentAmount);

        Assert.Equal([-12.5m, 1.8594m, 50m], query.AsEnumerable().Select(r => r.Spent.Amount).ToArray());
        var plan = Plan(db, query.ToQueryString());
        Assert.Contains("USING COVERING INDEX IX_Refuels_SpentCurrency_SpentAmount (SpentCurrency=?)", plan);
        Assert.DoesNotContain("TEMP B-TREE", plan);
    }

    private static SqliteConnection Connection(DbContext db)
    {
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        connection.Open();
        return connection;
    }

    private static string Plan(DbContext db, string queryString) =>
        Connection(db).QueryPlan(queryString[queryString.IndexOf("SELECT", StringComparison.Ordinal)..]);
}
