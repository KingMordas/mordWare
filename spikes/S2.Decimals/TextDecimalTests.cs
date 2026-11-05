using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace S2.Decimals;

/// <summary>
/// Design D2 / task 2.1: what EF Core 10 does with a plain decimal (stored as TEXT) on SQLite. This is the
/// evidence for rejecting it: results through EF are right, but only via a collation and a function EF
/// registers on its own connection, so the index is never used for ranges and plain SQL sorts the values as text.
/// </summary>
public sealed class TextDecimalTests : IDisposable
{
    public sealed class Row
    {
        public int Id { get; set; }
        public decimal Value { get; set; }
    }

    private sealed class Ctx(string cs) : DbContext
    {
        public DbSet<Row> Rows => Set<Row>();
        protected override void OnConfiguring(DbContextOptionsBuilder b) => b.UseSqlite(cs);
        protected override void OnModelCreating(ModelBuilder b) => b.Entity<Row>().HasIndex(r => r.Value).IsUnique();
    }

    private readonly TempFolder _temp = new();
    private readonly string _cs;

    public TextDecimalTests()
    {
        _cs = SpikeDb.ConnectionString(_temp.File("text.db"), SpikeDb.NewRawKey());
        using var db = new Ctx(_cs);
        db.Database.EnsureCreated();
        db.Rows.AddRange(new Row { Value = 9m }, new Row { Value = 10m }, new Row { Value = 100.5m }, new Row { Value = -2m });
        db.SaveChanges();
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Column_is_TEXT()
    {
        using var db = new Ctx(_cs);
        Assert.Contains("\"Value\" TEXT NOT NULL", db.Database.GenerateCreateScript());
        Assert.Equal("text", Raw(db, "SELECT DISTINCT typeof(Value) FROM Rows"));
    }

    [Fact]
    public void Linq_results_are_correct_through_EF_helpers()
    {
        using var db = new Ctx(_cs);

        Assert.Equal([-2m, 9m, 10m, 100.5m], db.Rows.OrderBy(r => r.Value).Select(r => r.Value).ToList());
        Assert.Equal([10m, 100.5m], db.Rows.Where(r => r.Value > 9.5m).OrderBy(r => r.Value).Select(r => r.Value).ToList());
        Assert.Equal(100.5m, db.Rows.Max(r => r.Value));
        Assert.Equal(-2m, db.Rows.Min(r => r.Value));
        Assert.Equal(117.5m, db.Rows.Sum(r => r.Value));

        // ...but only because EF rewrites the SQL around the column.
        Assert.Contains("COLLATE EF_DECIMAL", db.Rows.OrderBy(r => r.Value).ToQueryString());
        Assert.Contains("ef_compare(\"r\".\"Value\"", db.Rows.Where(r => r.Value > 9.5m).ToQueryString());
    }

    [Fact]
    public void Range_filter_scans_the_table_instead_of_using_the_index()
    {
        using var db = new Ctx(_cs);
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        connection.Open();

        var plan = connection.QueryPlan("SELECT Id FROM Rows AS r WHERE ef_compare(r.Value, '9.5') > 0");

        Assert.Contains("SCAN", plan);
        Assert.DoesNotContain("INDEX", plan);
    }

    [Fact]
    public void Plain_SQL_sorts_the_values_as_text()
    {
        using var db = new Ctx(_cs);

        // What DB Browser, a raw query or a future tool sees: 9.0 sorts after 100.5.
        Assert.Equal("-2.0,10.0,100.5,9.0", Raw(db, "SELECT group_concat(Value, ',') FROM (SELECT Value FROM Rows ORDER BY Value)"));
    }

    [Fact]
    public void Unique_index_holds_only_because_EF_normalizes_the_text()
    {
        using var db = new Ctx(_cs);

        // EF writes 100.50m as "100.5", so the duplicate collides...
        db.Rows.Add(new Row { Value = 100.50m });
        var ex = Assert.Throws<DbUpdateException>(() => db.SaveChanges());
        Assert.Equal(19, Assert.IsType<SqliteException>(ex.InnerException).SqliteErrorCode);

        // ...but a writer that does not normalize (raw SQL, an import script) slips past it.
        Assert.Equal(1, Exec(db, "INSERT INTO Rows (Value) VALUES ('100.50')"));
    }

    private static string? Raw(Ctx db, string sql)
    {
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        connection.Open();
        return connection.Scalar(sql)?.ToString();
    }

    private static int Exec(Ctx db, string sql)
    {
        db.ChangeTracker.Clear();
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteNonQuery();
    }
}
