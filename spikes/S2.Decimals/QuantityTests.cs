namespace S2.Decimals;

/// <summary>Task 3.1: construction, exact conversion and comparison of the spike value types (design D3).</summary>
public class QuantityTests
{
    [Fact]
    public void Each_type_stores_its_value_times_its_scale()
    {
        Assert.Equal(1234510L, Km.Of(12345.10m).Scaled);
        Assert.Equal(45678L, Liters.Of(45.678m).Scaled);
        Assert.Equal(1234567L, Kwh.Of(1234.567m).Scaled);
        Assert.Equal(25000L, RatePercent.Of(2.5m).Scaled);
        Assert.Equal(108_765_432L, FxRate.Of(1.08765432m).Scaled);
        Assert.Equal(18_590L, Money.Of(1.859m, "EUR").ScaledAmount);
    }

    [Fact]
    public void Conversion_back_to_decimal_is_exact()
    {
        Assert.Equal(12345.10m, Km.Of(12345.10m).Value);
        Assert.Equal(45.678m, Liters.FromScaled(45678).Value);
        Assert.Equal(1.08765432m, FxRate.FromScaled(108_765_432).Value);
        Assert.Equal(-0.0001m, Money.FromScaled(-1, "EUR").Amount);

        // Equality is numeric, not textual: 12345.1 and 12345.10 are the same reading.
        Assert.Equal(Km.Of(12345.1m), Km.Of(12345.10m));
    }

    [Fact]
    public void Comparison_follows_the_numbers()
    {
        Assert.True(Km.Of(9m) < Km.Of(100.5m));
        Assert.True(Liters.Of(10m) >= Liters.Of(9.999m));
        Assert.True(RatePercent.Of(-1m) < RatePercent.Of(0m));
        Assert.True(Money.Of(-2m, "EUR") < Money.Of(1m, "EUR"));

        Km[] sorted = [Km.Of(100.5m), Km.Of(9m), Km.Of(10m)];
        Array.Sort(sorted);
        Assert.Equal([Km.Of(9m), Km.Of(10m), Km.Of(100.5m)], sorted);
    }

    [Fact]
    public void Money_refuses_to_compare_or_add_different_currencies()
    {
        var eur = Money.Of(1m, "EUR");
        var jpy = Money.Of(1m, "JPY");

        Assert.Throws<InvalidOperationException>(() => eur < jpy);
        Assert.Throws<InvalidOperationException>(() => eur.CompareTo(jpy));
        Assert.Throws<InvalidOperationException>(() => eur + jpy);
        Assert.NotEqual(eur, jpy); // equality is fine: different currencies are simply not equal
    }

    [Theory]
    [InlineData("eur")]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("")]
    public void Money_needs_an_ISO_currency_code(string currency) =>
        Assert.Throws<ArgumentException>(() => Money.Of(1m, currency));
}
