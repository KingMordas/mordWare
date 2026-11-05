namespace S1.Encryption;

/// <summary>Design D6: generate, display and forgivingly parse the recovery key.</summary>
public class RecoveryKeyTests
{
    [Fact]
    public void Generate_format_parse_round_trips()
    {
        for (var i = 0; i < 200; i++)
        {
            var key = RecoveryKey.Generate();
            Assert.Equal(30, key.Length);
            Assert.All(key, c => Assert.Contains(c, RecoveryKey.Alphabet));

            var shown = RecoveryKey.Format(key);
            Assert.Matches("^[0-9A-Z]{5}(-[0-9A-Z]{5}){5}$", shown);

            Assert.True(RecoveryKey.TryParse(shown, out var parsed));
            Assert.Equal(key, parsed);
        }
    }

    [Fact]
    public void Alphabet_has_32_symbols_without_ambiguous_letters()
    {
        Assert.Equal(32, RecoveryKey.Alphabet.Distinct().Count());
        Assert.DoesNotContain('I', RecoveryKey.Alphabet);
        Assert.DoesNotContain('L', RecoveryKey.Alphabet);
        Assert.DoesNotContain('O', RecoveryKey.Alphabet);
        Assert.DoesNotContain('U', RecoveryKey.Alphabet);
    }

    [Fact]
    public void Lower_case_spaces_and_ambiguous_letters_normalize()
    {
        const string canonical = "10ABCDEFGH1JKMNPQRST0VWXYZ2345";
        Assert.Equal(30, canonical.Length);

        // lower case, spaces instead of hyphens, I/L for 1 and O for 0
        Assert.True(RecoveryKey.TryParse("iOabc defgh LjkmN pqrst OvwxY z2345", out var parsed));
        Assert.Equal(canonical, parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345-12345-12345-12345-12345")]            // 25 symbols
    [InlineData("12345-12345-12345-12345-12345-123456")]     // 31 symbols
    [InlineData("12345-12345-12345-12345-12345-1234U")]      // U is not in the alphabet
    [InlineData("12345-12345-12345-12345-12345-1234!")]
    public void Malformed_input_is_rejected(string input)
    {
        Assert.False(RecoveryKey.TryParse(input, out _));
    }
}
