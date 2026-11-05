namespace S2.Decimals;

/// <summary>
/// Task 3.3 / design D3: rounding is always explicit. Storage keeps 4 decimals; display rounds to the
/// currency's exponent with a mode the caller chooses (which mode is morFinance's decision).
/// </summary>
public class MoneyRoundingTests
{
    [Theory]
    // currency, stored amount, half-even, half-away-from-zero
    [InlineData("EUR", "2.125", "2.12", "2.13")]
    [InlineData("EUR", "2.135", "2.14", "2.14")]
    [InlineData("EUR", "-2.125", "-2.12", "-2.13")]
    [InlineData("JPY", "2.5", "2", "3")]
    [InlineData("JPY", "3.5", "4", "4")]
    [InlineData("KWD", "1.2345", "1.234", "1.235")]
    public void Display_rounding_follows_the_currency_exponent_and_the_chosen_mode(
        string currency, string stored, string halfEven, string awayFromZero)
    {
        var money = Money.Of(decimal.Parse(stored, System.Globalization.CultureInfo.InvariantCulture), currency);

        Assert.Equal(Dec(halfEven), money.RoundForDisplay(MidpointRounding.ToEven));
        Assert.Equal(Dec(awayFromZero), money.RoundForDisplay(MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void Storage_keeps_four_decimals_whatever_the_currency_exponent()
    {
        // A computed price per liter keeps 4 decimals in EUR, and even in JPY.
        Assert.Equal(1.8590m, Money.Of(1.859m, "EUR").Amount);
        Assert.Equal(0.5m, Money.Of(0.5m, "JPY").Amount);
    }

    [Fact]
    public void Price_per_unit_rounds_explicitly_to_storage_scale()
    {
        // 50.00 EUR for 26.89 l = 1.859427... EUR/l
        var price = Money.Of(50.00m, "EUR").Per(26.89m, MidpointRounding.ToEven);

        Assert.Equal(1.8594m, price.Amount);
        Assert.Equal(18_594L, price.ScaledAmount);
    }

    private static decimal Dec(string s) => decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
}
