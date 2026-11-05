namespace S2.Decimals;

/// <summary>Task 3.2 / design D8: edge values for every spike type.</summary>
public class EdgeTests
{
    // Factories by name, so one theory covers every type.
    private static readonly Dictionary<string, Func<decimal, decimal>> RoundTrip = new()
    {
        ["Km"] = v => Km.Of(v).Value,
        ["Liters"] = v => Liters.Of(v).Value,
        ["Kwh"] = v => Kwh.Of(v).Value,
        ["RatePercent"] = v => RatePercent.Of(v).Value,
        ["FxRate"] = v => FxRate.Of(v).Value,
        ["Money"] = v => Money.Of(v, "EUR").Amount,
    };

    public static TheoryData<string, decimal> ExactValues => new()
    {
        // smallest unit
        { "Km", 0.01m }, { "Liters", 0.001m }, { "Kwh", 0.001m },
        { "RatePercent", 0.0001m }, { "FxRate", 0.00000001m }, { "Money", 0.0001m },
        // zero (not valid for FxRate, see below)
        { "Km", 0m }, { "Liters", 0m }, { "Kwh", 0m }, { "RatePercent", 0m }, { "Money", 0m },
        // large realistic values
        { "Km", 999_999.99m }, { "Liters", 99_999.999m }, { "Kwh", 9_999_999.999m },
        { "RatePercent", 999.9999m }, { "FxRate", 160_000.12345678m }, { "Money", 1_000_000_000.00m },
        // negatives where allowed
        { "RatePercent", -0.5m }, { "Money", -1_234.5678m },
    };

    [Theory]
    [MemberData(nameof(ExactValues))]
    public void Values_within_scale_round_trip_exactly(string type, decimal value) =>
        Assert.Equal(value, RoundTrip[type](value));

    public static TheoryData<string, decimal> OneDecimalTooMany => new()
    {
        { "Km", 0.001m }, { "Liters", 0.0001m }, { "Kwh", 1.0005m },
        { "RatePercent", 2.50001m }, { "FxRate", 1.000000001m }, { "Money", 1.85901m },
    };

    [Theory]
    [MemberData(nameof(OneDecimalTooMany))]
    public void One_decimal_too_many_throws_instead_of_rounding(string type, decimal value) =>
        Assert.Throws<ArgumentException>(() => RoundTrip[type](value));

    public static TheoryData<string, decimal> InvalidSign => new()
    {
        { "Km", -0.01m }, { "Liters", -1m }, { "Kwh", -0.001m }, { "FxRate", -1m }, { "FxRate", 0m },
    };

    [Theory]
    [MemberData(nameof(InvalidSign))]
    public void Negative_values_are_rejected_except_for_money_and_rates(string type, decimal value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RoundTrip[type](value));

    [Fact]
    public void Int64_boundaries_hold_and_throw_just_past_them()
    {
        // Largest value per scale: long.MaxValue / 10^s, exactly representable.
        var maxKm = (decimal)long.MaxValue / 100;          //  92,233,720,368,547,758.07
        var maxMoney = (decimal)long.MaxValue / 10_000;    //     922,337,203,685,477.5807
        var minMoney = (decimal)long.MinValue / 10_000;
        var maxFx = (decimal)long.MaxValue / 100_000_000;  //          92,233,720,368.54775807

        Assert.Equal(long.MaxValue, Km.Of(maxKm).Scaled);
        Assert.Equal(long.MaxValue, Money.Of(maxMoney, "EUR").ScaledAmount);
        Assert.Equal(long.MinValue, Money.Of(minMoney, "EUR").ScaledAmount);
        Assert.Equal(long.MaxValue, FxRate.Of(maxFx).Scaled);

        Assert.Throws<ArgumentOutOfRangeException>(() => Km.Of(maxKm + 0.01m));
        Assert.Throws<ArgumentOutOfRangeException>(() => Money.Of(maxMoney + 0.0001m, "EUR"));
        Assert.Throws<ArgumentOutOfRangeException>(() => Money.Of(minMoney - 0.0001m, "EUR"));
        Assert.Throws<ArgumentOutOfRangeException>(() => FxRate.Of(maxFx + 0.00000001m));

        // Far past the range: decimal itself would overflow while scaling; still a range error.
        Assert.Throws<ArgumentOutOfRangeException>(() => FxRate.Of(decimal.MaxValue));

        // Arithmetic overflow is checked, never wrapped.
        Assert.Throws<OverflowException>(() => Money.Of(maxMoney, "EUR") + Money.Of(0.0001m, "EUR"));
    }

    public static TheoryData<decimal> OldDecimal18_2 => new()
    {
        0.01m, 266.85m, 12_345.67m, -98_765.43m, 9_999_999_999_999_999.99m, -9_999_999_999_999_999.99m,
    };

    [Theory]
    [MemberData(nameof(OldDecimal18_2))]
    public void Old_decimal_18_2_values_convert_exactly(decimal value)
    {
        // Every decimal(18,2) fits Km (x100) exactly, over the column's whole range.
        if (value >= 0)
        {
            Assert.Equal(value, Km.Of(value).Value);
        }

        // Money at x10^4 holds +/-922 trillion: every realistic amount converts exactly...
        if (Math.Abs(value) < 922_000_000_000_000m)
        {
            Assert.Equal(value, Money.Of(value, "EUR").Amount);
        }
        else
        {
            // ...while the extremes of the old column's *declared* range are refused, never truncated.
            Assert.Throws<ArgumentOutOfRangeException>(() => Money.Of(value, "EUR"));
        }
    }

    public static TheoryData<decimal> OldDecimal18_3 => new()
    {
        0.001m, 45.678m, 1_234.567m, 999_999_999_999_999.999m,
    };

    [Theory]
    [MemberData(nameof(OldDecimal18_3))]
    public void Old_decimal_18_3_values_convert_exactly(decimal value)
    {
        Assert.Equal(value, Liters.Of(value).Value);
        Assert.Equal(value, Kwh.Of(value).Value);
    }
}
