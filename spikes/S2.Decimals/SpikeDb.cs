using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace S2.Decimals;

/// <summary>
/// The encrypted connection proven in S1 (SQLCipher v4 scheme, raw 256-bit key), copied so S2 does not depend
/// on S1 (design D1). Every S2 database is opened this way.
/// </summary>
internal static class SpikeDb
{
    public static string CipherUri(string path) =>
        new Uri(Path.GetFullPath(path)).AbsoluteUri + "?cipher=sqlcipher&legacy=4";

    /// <summary>A fresh throwaway raw key in SQLCipher syntax: x'&lt;64 hex&gt;'.</summary>
    public static string NewRawKey() => "x'" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) + "'";

    public static string ConnectionString(string path, string rawKey) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = CipherUri(path),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            Password = rawKey,
        }.ToString();

    public static SqliteConnection Open(string path, string rawKey)
    {
        var connection = new SqliteConnection(ConnectionString(path, rawKey));
        connection.Open();
        return connection;
    }

    public static object? Scalar(this SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    /// <summary>EXPLAIN QUERY PLAN for <paramref name="sql"/>, details joined; parameters are bound to NULL.</summary>
    public static string QueryPlan(this SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + sql;
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(sql, @"@\w+"))
        {
            if (!command.Parameters.Contains(m.Value))
            {
                command.Parameters.AddWithValue(m.Value, 0L);
            }
        }
        using var reader = command.ExecuteReader();
        var lines = new List<string>();
        while (reader.Read())
        {
            lines.Add(reader.GetString(3));
        }
        return string.Join(" | ", lines);
    }
}
