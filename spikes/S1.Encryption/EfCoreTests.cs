using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace S1.Encryption;

/// <summary>Design D2: the whole EF Core path (migrations included) runs on SQLitePCLRaw.core 3.x + SQLite3MC.</summary>
public class EfCoreTests
{
    [Fact]
    public void Encrypted_database_migrates_writes_and_reads()
    {
        using var temp = new TempFolder();
        var path = temp.File("efcore.db");
        const string password = "spike-ef-password";

        using (var db = new NotesContext(SpikeDb.ConnectionString(path, password)))
        {
            db.Database.Migrate();
            db.Notes.Add(new Note { Text = "first", Tag = "a" });
            db.Notes.Add(new Note { Text = "second", Tag = null });
            db.SaveChanges();
        }

        using (var db = new NotesContext(SpikeDb.ConnectionString(path, password)))
        {
            db.Database.Migrate(); // second run: nothing pending, nothing re-applied

            Assert.Equal(["first", "second"], db.Notes.OrderBy(n => n.Id).Select(n => n.Text).ToArray());
            Assert.Equal(
                ["20261104000001_CreateNotes", "20261104000002_AddNoteTag"],
                db.Database.GetAppliedMigrations().ToArray());
            Assert.Empty(db.Database.GetPendingMigrations());
        }
    }
}

public sealed class Note
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
    public string? Tag { get; set; }
}

public sealed class NotesContext(string connectionString) : DbContext
{
    public DbSet<Note> Notes => Set<Note>();

    protected override void OnConfiguring(DbContextOptionsBuilder options) =>
        options
            .UseSqlite(connectionString)
            // Hand-written migrations have no model snapshot; EF Core 9+ would otherwise refuse to migrate.
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
}

[DbContext(typeof(NotesContext))]
[Migration("20261104000001_CreateNotes")]
public sealed class CreateNotes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: "Notes",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Text = table.Column<string>(type: "TEXT", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_Notes", x => x.Id));
}

[DbContext(typeof(NotesContext))]
[Migration("20261104000002_AddNoteTag")]
public sealed class AddNoteTag : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(name: "Tag", table: "Notes", type: "TEXT", nullable: true);
}
