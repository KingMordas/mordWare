using Microsoft.Data.Sqlite;

namespace S1.Encryption;

/// <summary>How the spike opens SQLite3MC databases with the SQLCipher v4 scheme (design D3).</summary>
internal static class SpikeDb
{
    /// <summary>
    /// SQLite URI for a file path: percent-encoded (spaces, '#', '%', non-ASCII) and carrying the
    /// cipher parameters, so the cipher is selected before the key is applied.
    /// </summary>
    public static string CipherUri(string path) =>
        new Uri(Path.GetFullPath(path)).AbsoluteUri + "?cipher=sqlcipher&legacy=4";

    /// <summary>Connection string for an encrypted database; <paramref name="password"/> null = no key at all.</summary>
    public static string ConnectionString(string path, string? password, SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = CipherUri(path),
            Mode = mode,
            Pooling = false, // the spike opens and closes files constantly; pooling would keep handles open
        };
        if (password is not null)
        {
            builder.Password = password;
        }
        return builder.ToString();
    }

    /// <summary>SQLCipher raw-key syntax for a 32-byte key: x'&lt;64 hex&gt;' (no KDF on open, design D4).</summary>
    public static string RawKey(ReadOnlySpan<byte> key) => "x'" + Convert.ToHexString(key) + "'";

    public static SqliteConnection Open(string path, string? password, SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate)
    {
        var connection = new SqliteConnection(ConnectionString(path, password, mode));
        connection.Open();
        return connection;
    }

    public static object? Scalar(this SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    public static int Execute(this SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteNonQuery();
    }
}
