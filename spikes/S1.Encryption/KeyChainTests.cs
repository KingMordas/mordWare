using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace S1.Encryption;

/// <summary>
/// Design D5: data key -> DPAPI wrap and recovery wrap -> data key discarded -> each unwrap opens the database.
/// Only throwaway keys and temp databases are used (design D9).
/// </summary>
[SupportedOSPlatform("windows")]
public class KeyChainTests
{
    [Fact]
    public void Both_unwraps_recover_the_data_key_and_open_the_database()
    {
        using var temp = new TempFolder();
        var path = temp.File("keychain.db");

        // 1. A new data key and an encrypted database.
        var dataKey = KeyWrap.NewDataKey();
        using (var connection = SpikeDb.Open(path, SpikeDb.RawKey(dataKey)))
        {
            connection.Execute("CREATE TABLE t(x); INSERT INTO t VALUES ('kept');");
        }

        // 2. Wrap it twice, as Foundation will on first run.
        var dpapiBlob = KeyWrap.ProtectWithDpapi(dataKey, SpikeEdition.Dev);
        var recoveryKey = RecoveryKey.Generate();
        var recoveryBlob = KeyWrap.WrapWithRecoveryKey(dataKey, recoveryKey, SpikeEdition.Dev);

        // 3. Discard the data key.
        CryptographicOperations.ZeroMemory(dataKey);

        // 4. Daily path: DPAPI.
        var fromDpapi = KeyWrap.UnprotectWithDpapi(dpapiBlob, SpikeEdition.Dev);
        using (var connection = SpikeDb.Open(path, SpikeDb.RawKey(fromDpapi)))
        {
            Assert.Equal("kept", connection.Scalar("SELECT x FROM t"));
        }

        // 5. Recovery path: the key as the user would type it back (lower case, display form).
        Assert.True(RecoveryKey.TryParse(RecoveryKey.Format(recoveryKey).ToLowerInvariant(), out var typed));
        var fromRecovery = KeyWrap.UnwrapWithRecoveryKey(recoveryBlob, typed, SpikeEdition.Dev);
        using (var connection = SpikeDb.Open(path, SpikeDb.RawKey(fromRecovery)))
        {
            Assert.Equal("kept", connection.Scalar("SELECT x FROM t"));
        }

        Assert.Equal(fromDpapi, fromRecovery);
    }

    [Fact]
    public void Wrong_recovery_key_fails()
    {
        var blob = KeyWrap.WrapWithRecoveryKey(KeyWrap.NewDataKey(), RecoveryKey.Generate(), SpikeEdition.Prod);

        Assert.ThrowsAny<CryptographicException>(() =>
            KeyWrap.UnwrapWithRecoveryKey(blob, RecoveryKey.Generate(), SpikeEdition.Prod));
    }

    [Theory]
    [InlineData(1)]                                   // salt
    [InlineData(1 + 16)]                              // nonce
    [InlineData(1 + 16 + 12)]                         // ciphertext
    [InlineData(KeyWrap.RecoveryBlobSize - 1)]        // tag
    public void Tampered_recovery_blob_fails(int offset)
    {
        var recoveryKey = RecoveryKey.Generate();
        var blob = KeyWrap.WrapWithRecoveryKey(KeyWrap.NewDataKey(), recoveryKey, SpikeEdition.Prod);
        blob[offset] ^= 0x01;

        Assert.ThrowsAny<CryptographicException>(() =>
            KeyWrap.UnwrapWithRecoveryKey(blob, recoveryKey, SpikeEdition.Prod));
    }

    [Fact]
    public void Unknown_recovery_blob_version_fails()
    {
        var recoveryKey = RecoveryKey.Generate();
        var blob = KeyWrap.WrapWithRecoveryKey(KeyWrap.NewDataKey(), recoveryKey, SpikeEdition.Prod);
        blob[0] = 2;

        Assert.ThrowsAny<CryptographicException>(() =>
            KeyWrap.UnwrapWithRecoveryKey(blob, recoveryKey, SpikeEdition.Prod));
    }

    [Fact]
    public void Prod_recovery_blob_does_not_unwrap_as_Dev_and_vice_versa()
    {
        var recoveryKey = RecoveryKey.Generate();
        var dataKey = KeyWrap.NewDataKey();

        var prodBlob = KeyWrap.WrapWithRecoveryKey(dataKey, recoveryKey, SpikeEdition.Prod);
        var devBlob = KeyWrap.WrapWithRecoveryKey(dataKey, recoveryKey, SpikeEdition.Dev);

        Assert.ThrowsAny<CryptographicException>(() => KeyWrap.UnwrapWithRecoveryKey(prodBlob, recoveryKey, SpikeEdition.Dev));
        Assert.ThrowsAny<CryptographicException>(() => KeyWrap.UnwrapWithRecoveryKey(devBlob, recoveryKey, SpikeEdition.Prod));

        // Stating the blob's own edition works (the PROD -> DEV restore path re-wraps after this).
        Assert.Equal(dataKey, KeyWrap.UnwrapWithRecoveryKey(prodBlob, recoveryKey, SpikeEdition.Prod));
    }

    [Fact]
    public void Prod_dpapi_blob_does_not_unprotect_as_Dev()
    {
        var blob = KeyWrap.ProtectWithDpapi(KeyWrap.NewDataKey(), SpikeEdition.Prod);

        Assert.ThrowsAny<CryptographicException>(() => KeyWrap.UnprotectWithDpapi(blob, SpikeEdition.Dev));
    }

    [Fact]
    public void Recovery_blob_has_fixed_size_and_fresh_randomness()
    {
        var recoveryKey = RecoveryKey.Generate();
        var dataKey = KeyWrap.NewDataKey();

        var a = KeyWrap.WrapWithRecoveryKey(dataKey, recoveryKey, SpikeEdition.Prod);
        var b = KeyWrap.WrapWithRecoveryKey(dataKey, recoveryKey, SpikeEdition.Prod);

        Assert.Equal(KeyWrap.RecoveryBlobSize, a.Length);
        Assert.Equal(77, a.Length);
        Assert.Equal(1, a[0]);
        Assert.NotEqual(a, b); // new salt and nonce every time
    }
}
