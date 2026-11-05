using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace S3.Storage.Probe;

// Own copy of the S1 helper (spikes/S1.Encryption/KeyWrap.cs): no spike references another (design D1).

/// <summary>
/// Design D5: the data key (DEK) is wrapped twice, both times bound to the edition.
/// - DPAPI (CurrentUser) for daily use, with no prompt.
/// - HKDF-SHA256 + AES-256-GCM under the recovery key, for restore on another PC or profile.
/// Base-library and Microsoft crypto only; no third-party crypto.
/// </summary>
internal static class KeyWrap
{
    public const int DataKeySize = 32;

    private const byte RecoveryBlobVersion = 1;
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    /// <summary>version | salt | nonce | ciphertext | tag</summary>
    public const int RecoveryBlobSize = 1 + SaltSize + NonceSize + DataKeySize + TagSize;

    private static readonly byte[] HkdfInfo = Encoding.UTF8.GetBytes("mordWare recovery-wrap v1");

    public static byte[] NewDataKey() => RandomNumberGenerator.GetBytes(DataKeySize);

    // ----- DPAPI -----

    [SupportedOSPlatform("windows")]
    public static byte[] ProtectWithDpapi(byte[] dataKey, SpikeEdition edition) =>
        ProtectedData.Protect(dataKey, DpapiEntropy(edition), DataProtectionScope.CurrentUser);

    /// <exception cref="CryptographicException">The blob was made for another edition, user or machine, or is damaged.</exception>
    [SupportedOSPlatform("windows")]
    public static byte[] UnprotectWithDpapi(byte[] blob, SpikeEdition edition) =>
        ProtectedData.Unprotect(blob, DpapiEntropy(edition), DataProtectionScope.CurrentUser);

    private static byte[] DpapiEntropy(SpikeEdition edition) =>
        Encoding.UTF8.GetBytes($"mordWare|{edition}|dek|v1");

    // ----- Recovery key -----

    public static byte[] WrapWithRecoveryKey(byte[] dataKey, string canonicalRecoveryKey, SpikeEdition edition)
    {
        if (dataKey.Length != DataKeySize)
        {
            throw new ArgumentException("The data key must be 32 bytes.", nameof(dataKey));
        }

        var blob = new byte[RecoveryBlobSize];
        var span = blob.AsSpan();
        span[0] = RecoveryBlobVersion;
        var salt = span.Slice(1, SaltSize);
        var nonce = span.Slice(1 + SaltSize, NonceSize);
        var ciphertext = span.Slice(1 + SaltSize + NonceSize, DataKeySize);
        var tag = span.Slice(1 + SaltSize + NonceSize + DataKeySize, TagSize);

        RandomNumberGenerator.Fill(salt);
        RandomNumberGenerator.Fill(nonce);

        var wrappingKey = DeriveWrappingKey(canonicalRecoveryKey, salt);
        try
        {
            using var aes = new AesGcm(wrappingKey, TagSize);
            aes.Encrypt(nonce, dataKey, ciphertext, tag, AssociatedData(edition));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrappingKey);
        }

        return blob;
    }

    /// <exception cref="CryptographicException">
    /// Wrong recovery key, wrong edition, or a tampered blob (AES-GCM tag mismatch); or an unknown blob version.
    /// </exception>
    public static byte[] UnwrapWithRecoveryKey(byte[] blob, string canonicalRecoveryKey, SpikeEdition edition)
    {
        if (blob.Length != RecoveryBlobSize || blob[0] != RecoveryBlobVersion)
        {
            throw new CryptographicException("Unknown recovery blob format.");
        }

        var span = blob.AsSpan();
        var salt = span.Slice(1, SaltSize);
        var nonce = span.Slice(1 + SaltSize, NonceSize);
        var ciphertext = span.Slice(1 + SaltSize + NonceSize, DataKeySize);
        var tag = span.Slice(1 + SaltSize + NonceSize + DataKeySize, TagSize);

        var dataKey = new byte[DataKeySize];
        var wrappingKey = DeriveWrappingKey(canonicalRecoveryKey, salt);
        try
        {
            using var aes = new AesGcm(wrappingKey, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, dataKey, AssociatedData(edition));
            return dataKey;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(dataKey);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrappingKey);
        }
    }

    private static byte[] DeriveWrappingKey(string canonicalRecoveryKey, ReadOnlySpan<byte> salt)
    {
        var ikm = Encoding.ASCII.GetBytes(canonicalRecoveryKey);
        try
        {
            var key = new byte[32];
            HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, key, salt, HkdfInfo);
            return key;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ikm);
        }
    }

    private static byte[] AssociatedData(SpikeEdition edition) =>
        Encoding.UTF8.GetBytes($"mordWare|{edition}|v1");
}
