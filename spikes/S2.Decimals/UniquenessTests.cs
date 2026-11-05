using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace S2.Decimals;

/// <summary>
/// Task 5.2 / design D6: a unique index on a scaled column is numeric. 12345.10 and 12345.1 are one reading,
/// whoever writes them (EF or raw SQL), because both are the integer 1234510.
/// </summary>
public sealed class UniquenessTests : IDisposable
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
            refuel.HasIndex(r => new { r.VehicleId, r.OdometerKm }).IsUnique();
        }
    }

    private readonly TempFolder _temp = new();
    private readonly string _cs;

    public UniquenessTests()
    {
        _cs = SpikeDb.ConnectionString(_temp.File("unique.db"), SpikeDb.NewRawKey());
        using var db = new Ctx(_cs);
        db.Database.EnsureCreated();
        db.Refuels.Add(new Refuel { VehicleId = 1, OdometerKm = Km.Of(12345.10m) });
        db.SaveChanges();
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Same_reading_with_a_different_number_of_decimals_collides()
    {
        using var db = new Ctx(_cs);
        db.Refuels.Add(new Refuel { VehicleId = 1, OdometerKm = Km.Of(12345.1m) });

        var ex = Assert.Throws<DbUpdateException>(() => db.SaveChanges());
        var sqlite = Assert.IsType<SqliteException>(ex.InnerException);
        Assert.Equal(19, sqlite.SqliteErrorCode);             // SQLITE_CONSTRAINT
        Assert.Equal(2067, sqlite.SqliteExtendedErrorCode);   // SQLITE_CONSTRAINT_UNIQUE
    }

    [Fact]
    public void Raw_SQL_cannot_slip_a_duplicate_past_the_index()
    {
        using var db = new Ctx(_cs);
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Refuels (VehicleId, OdometerKm) VALUES (1, 1234510)";

        Assert.Equal(19, Assert.Throws<SqliteException>(() => command.ExecuteNonQuery()).SqliteErrorCode);
    }

    [Fact]
    public void A_different_reading_or_another_vehicle_is_accepted()
    {
        using var db = new Ctx(_cs);
        db.Refuels.Add(new Refuel { VehicleId = 1, OdometerKm = Km.Of(12345.11m) });
        db.Refuels.Add(new Refuel { VehicleId = 2, OdometerKm = Km.Of(12345.10m) });

        Assert.Equal(2, db.SaveChanges());
        Assert.Equal(3, db.Refuels.Count());
    }
}
