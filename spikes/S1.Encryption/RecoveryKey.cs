using System.Security.Cryptography;
using System.Text;

namespace S1.Encryption;

/// <summary>
/// Design D6: a generated recovery key of 30 Crockford base32 symbols (150 bits), shown as six groups of five.
/// Never user-chosen, which is what lets the recovery wrap use HKDF instead of a slow password KDF (design D5).
/// </summary>
internal static class RecoveryKey
{
    /// <summary>Crockford base32: digits and letters without I, L, O, U.</summary>
    public const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public const int Length = 30;
    public const int GroupSize = 5;

    /// <summary>A new key in canonical form (30 upper-case symbols, no separators).</summary>
    public static string Generate()
    {
        var symbols = new char[Length];
        for (var i = 0; i < Length; i++)
        {
            symbols[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }
        return new string(symbols);
    }

    /// <summary>Display form: XXXXX-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX.</summary>
    public static string Format(string canonical) =>
        string.Join('-', Enumerable.Range(0, Length / GroupSize).Select(g => canonical.Substring(g * GroupSize, GroupSize)));

    /// <summary>
    /// Forgiving parse: case-insensitive, spaces and hyphens ignored, I/L read as 1 and O as 0.
    /// Returns false for a wrong length or a symbol outside the alphabet (for example U).
    /// </summary>
    public static bool TryParse(string? input, out string canonical)
    {
        canonical = "";
        if (input is null)
        {
            return false;
        }

        var builder = new StringBuilder(Length);
        foreach (var raw in input)
        {
            if (raw is ' ' or '-')
            {
                continue;
            }

            var c = char.ToUpperInvariant(raw) switch
            {
                'I' or 'L' => '1',
                'O' => '0',
                var other => other,
            };
            if (Alphabet.IndexOf(c) < 0)
            {
                return false;
            }
            builder.Append(c);
        }

        if (builder.Length != Length)
        {
            return false;
        }

        canonical = builder.ToString();
        return true;
    }
}
