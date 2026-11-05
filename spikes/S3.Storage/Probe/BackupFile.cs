using System.Buffers.Binary;
using System.Security.Cryptography;

namespace S3.Storage.Probe;

/// <summary>
/// Design D3: the draft backup header (spike only; Foundation finalizes the format).
/// magic "MWBK" 4 | format version 2 | edition 1 | schema version 4 | created UTC ticks 8 |
/// recovery blob 77 | payload SHA-256 32 | payload (the encrypted SQLite file, unchanged).
/// All integers little-endian. The header is plaintext; tampering with the edition byte makes the
/// recovery unwrap fail, because the wrap is bound to the edition (S1).
/// </summary>
internal sealed record BackupHeader(ushort FormatVersion, SpikeEdition Edition, uint SchemaVersion, DateTime CreatedUtc, byte[] RecoveryBlob, byte[] PayloadSha256)
{
    public static ReadOnlySpan<byte> Magic => "MWBK"u8;

    public const ushort CurrentFormatVersion = 1;

    public const int EditionOffset = 4 + 2;

    public const int Size = 4 + 2 + 1 + 4 + 8 + KeyWrap.RecoveryBlobSize + 32;

    public byte[] ToBytes()
    {
        var bytes = new byte[Size];
        var span = bytes.AsSpan();
        Magic.CopyTo(span);
        BinaryPrimitives.WriteUInt16LittleEndian(span[4..], FormatVersion);
        span[EditionOffset] = (byte)Edition;
        BinaryPrimitives.WriteUInt32LittleEndian(span[7..], SchemaVersion);
        BinaryPrimitives.WriteInt64LittleEndian(span[11..], CreatedUtc.Ticks);
        RecoveryBlob.CopyTo(span[19..]);
        PayloadSha256.CopyTo(span[(19 + KeyWrap.RecoveryBlobSize)..]);
        return bytes;
    }

    /// <exception cref="InvalidDataException">Not a backup file, or an unknown format version or edition.</exception>
    public static BackupHeader Parse(ReadOnlySpan<byte> span)
    {
        if (span.Length < Size || !span[..4].SequenceEqual(Magic))
        {
            throw new InvalidDataException("Not a mordWare backup file (magic).");
        }

        var version = BinaryPrimitives.ReadUInt16LittleEndian(span[4..]);
        if (version != CurrentFormatVersion)
        {
            throw new InvalidDataException($"Unknown backup format version {version}.");
        }

        var editionByte = span[EditionOffset];
        if (!Enum.IsDefined(typeof(SpikeEdition), (int)editionByte))
        {
            throw new InvalidDataException($"Unknown edition {editionByte}.");
        }

        return new BackupHeader(
            version,
            (SpikeEdition)editionByte,
            BinaryPrimitives.ReadUInt32LittleEndian(span[7..]),
            new DateTime(BinaryPrimitives.ReadInt64LittleEndian(span[11..]), DateTimeKind.Utc),
            span.Slice(19, KeyWrap.RecoveryBlobSize).ToArray(),
            span.Slice(19 + KeyWrap.RecoveryBlobSize, 32).ToArray());
    }
}

/// <summary>Design D3: write sequence and restore flow.</summary>
internal static class BackupFile
{
    public const string Extension = ".s3bak";

    /// <summary>
    /// SQLite backup API into &lt;name&gt;.db.tmp (keyed with the same data key), then header + bytes
    /// into &lt;name&gt;.s3bak.tmp with write-through, then rename to &lt;name&gt;.s3bak. Every step stays in
    /// <paramref name="folder"/>, so the rename never crosses volumes. With <paramref name="interrupt"/>
    /// the process exits after the .s3bak.tmp is written and before the rename.
    /// </summary>
    public static string Write(string folder, SpikeEdition edition, Action<Dictionary<string, object?>> report, bool interrupt, Dictionary<string, object?> result)
    {
        Directory.CreateDirectory(folder);

        // Leftovers of an interrupted run are never valid backups; the next run removes them.
        var leftovers = Directory.GetFiles(folder, "*.tmp");
        foreach (var leftover in leftovers)
        {
            File.Delete(leftover);
        }
        result["removedLeftovers"] = leftovers.Select(Path.GetFileName).ToArray();

        var name = $"spike-{edition.ToString().ToUpperInvariant()}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}";
        var dbTmp = Path.Combine(folder, name + ".db.tmp");
        var bakTmp = Path.Combine(folder, name + Extension + ".tmp");
        var bak = Path.Combine(folder, name + Extension);

        var dataKey = SpikeDatabase.LoadDataKey(edition);
        var recoveryBlob = File.ReadAllBytes(SpikeDatabase.RecoveryBlobPath);
        try
        {
            using (var source = SpikeDb.Open(SpikeDatabase.DatabasePath, SpikeDb.RawKey(dataKey)))
            using (var destination = SpikeDb.Open(dbTmp, SpikeDb.RawKey(dataKey)))
            {
                source.BackupDatabase(destination);
            }

            var payload = File.ReadAllBytes(dbTmp);
            var header = new BackupHeader(BackupHeader.CurrentFormatVersion, edition, 0, DateTime.UtcNow, recoveryBlob, SHA256.HashData(payload));

            using (var stream = new FileStream(bakTmp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16, FileOptions.WriteThrough))
            {
                stream.Write(header.ToBytes());
                stream.Write(payload);
                stream.Flush(flushToDisk: true);
            }

            if (interrupt)
            {
                result["interrupted"] = true;
                result["leftBehind"] = new[] { Path.GetFileName(dbTmp), Path.GetFileName(bakTmp) };
                report(result);
                Environment.Exit(3);
            }

            File.Move(bakTmp, bak);
            File.Delete(dbTmp);
            result["payloadBytes"] = payload.Length;
            return bak;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <summary>The newest complete backup in a folder; .tmp files are never candidates.</summary>
    public static string Newest(string folder) =>
        Directory.GetFiles(folder, "*" + Extension)
            .Where(f => f.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
            .FirstOrDefault()
        ?? throw new FileNotFoundException($"No {Extension} file in {folder}.");

    /// <summary>
    /// Restore flow (design D3): header -> payload hash -> edition rule -> unwrap with the recovery key
    /// under the header's edition -> write and check a .restore.tmp copy -> replace the live database ->
    /// re-wrap the data key (DPAPI and recovery) under the running edition.
    /// The live database is untouched until the restored copy has opened and checked out.
    /// </summary>
    public static void Restore(string file, string recoveryKeyInput, SpikeEdition running, Dictionary<string, object?> result)
    {
        if (!RecoveryKey.TryParse(recoveryKeyInput, out var recoveryKey))
        {
            throw new ArgumentException("The recovery key is not well-formed.");
        }

        var bytes = File.ReadAllBytes(file);
        var header = BackupHeader.Parse(bytes);
        result["headerEdition"] = header.Edition.ToString();
        result["headerCreatedUtc"] = header.CreatedUtc.ToString("O");

        var payload = bytes.AsSpan(BackupHeader.Size);
        if (!SHA256.HashData(payload).AsSpan().SequenceEqual(header.PayloadSha256))
        {
            throw new InvalidDataException("The backup is damaged or incomplete (payload hash mismatch).");
        }

        // Edition rule (Foundation notes): same edition ok; PROD -> DEV ok; DEV -> PROD refused.
        if (header.Edition == SpikeEdition.Dev && running == SpikeEdition.Prod)
        {
            throw new InvalidOperationException("A DEV backup cannot be restored into PROD.");
        }

        var dataKey = KeyWrap.UnwrapWithRecoveryKey(header.RecoveryBlob, recoveryKey, header.Edition);
        try
        {
            var restoreTmp = SpikeDatabase.DatabasePath + ".restore.tmp";
            File.WriteAllBytes(restoreTmp, payload.ToArray());

            using (var check = SpikeDb.Open(restoreTmp, SpikeDb.RawKey(dataKey)))
            {
                result["quickCheck"] = check.Scalar("PRAGMA quick_check") as string;
                result["restoredRows"] = Convert.ToInt64(check.Scalar("SELECT COUNT(*) FROM Items"));
            }

            if (result["quickCheck"] as string != "ok")
            {
                File.Delete(restoreTmp);
                throw new InvalidDataException("The restored database failed its integrity check.");
            }

            foreach (var sidecar in new[] { "-wal", "-shm" })
            {
                File.Delete(SpikeDatabase.DatabasePath + sidecar);
            }
            File.Move(restoreTmp, SpikeDatabase.DatabasePath, overwrite: true);

            SpikeDatabase.SaveKeys(dataKey, recoveryKey, running);
            result["rewrappedAs"] = running.ToString();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }
}
