using System.Globalization;

namespace S2.Decimals;

/// <summary>
/// A decimal quantity stored as a signed 64-bit integer at a fixed scale (design D3). The static members let
/// one generic converter (<see cref="ScaledConverter{T}"/>) serve every type, taking the scale from the type
/// itself so a column can never be mapped with a hand-written scale.
/// </summary>
public interface IScaled<TSelf> : IComparable<TSelf> where TSelf : struct, IScaled<TSelf>
{
    /// <summary>Number of decimals kept; the stored integer is value x 10^Decimals.</summary>
    static abstract int Decimals { get; }

    long Scaled { get; }

    static abstract TSelf FromScaled(long scaled);
}

public static class Scaling
{
    public enum Sign { Any, NonNegative, Positive }

    public static long Factor(int decimals) => decimals switch
    {
        2 => 100L,
        3 => 1_000L,
        4 => 10_000L,
        8 => 100_000_000L,
        _ => throw new ArgumentOutOfRangeException(nameof(decimals)),
    };

    /// <summary>Exact decimal -> scaled integer. Throws rather than rounds: storage never rounds silently.</summary>
    public static long ToScaled(decimal value, int decimals, Sign sign, string type)
    {
        if (sign == Sign.NonNegative && value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"{type} cannot be negative.");
        }
        if (sign == Sign.Positive && value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"{type} must be positive.");
        }

        decimal scaled;
        try
        {
            scaled = value * Factor(decimals);
        }
        catch (OverflowException e)
        {
            throw new ArgumentOutOfRangeException($"{type} is out of range.", e);
        }

        if (scaled != decimal.Truncate(scaled))
        {
            throw new ArgumentException($"{type} has more than {decimals} decimals.", nameof(value));
        }
        if (scaled > long.MaxValue || scaled < long.MinValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"{type} is out of range.");
        }
        return (long)scaled;
    }

    /// <summary>Exact scaled integer -> decimal: dividing by a power of ten only sets the decimal's scale.</summary>
    public static decimal ToDecimal(long scaled, int decimals) => (decimal)scaled / Factor(decimals);

    public static void RequireSign(long scaled, Sign sign, string type)
    {
        if ((sign == Sign.NonNegative && scaled < 0) || (sign == Sign.Positive && scaled <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(scaled), $"{type} has an invalid sign.");
        }
    }
}

/// <summary>Odometer readings and km intervals: x100 (morVehicles Domain in production).</summary>
public readonly record struct Km : IScaled<Km>
{
    private const Scaling.Sign Sign = Scaling.Sign.NonNegative;
    private Km(long scaled) => Scaled = scaled;

    public static int Decimals => 2;
    public long Scaled { get; }
    public decimal Value => Scaling.ToDecimal(Scaled, Decimals);

    public static Km Of(decimal value) => new(Scaling.ToScaled(value, Decimals, Sign, nameof(Km)));
    public static Km FromScaled(long scaled) { Scaling.RequireSign(scaled, Sign, nameof(Km)); return new(scaled); }

    public int CompareTo(Km other) => Scaled.CompareTo(other.Scaled);
    public static bool operator <(Km a, Km b) => a.Scaled < b.Scaled;
    public static bool operator >(Km a, Km b) => a.Scaled > b.Scaled;
    public static bool operator <=(Km a, Km b) => a.Scaled <= b.Scaled;
    public static bool operator >=(Km a, Km b) => a.Scaled >= b.Scaled;
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Fuel and (same shape) pantry quantities: x1000 (morVehicles Domain in production).</summary>
public readonly record struct Liters : IScaled<Liters>
{
    private const Scaling.Sign Sign = Scaling.Sign.NonNegative;
    private Liters(long scaled) => Scaled = scaled;

    public static int Decimals => 3;
    public long Scaled { get; }
    public decimal Value => Scaling.ToDecimal(Scaled, Decimals);

    public static Liters Of(decimal value) => new(Scaling.ToScaled(value, Decimals, Sign, nameof(Liters)));
    public static Liters FromScaled(long scaled) { Scaling.RequireSign(scaled, Sign, nameof(Liters)); return new(scaled); }

    public int CompareTo(Liters other) => Scaled.CompareTo(other.Scaled);
    public static bool operator <(Liters a, Liters b) => a.Scaled < b.Scaled;
    public static bool operator >(Liters a, Liters b) => a.Scaled > b.Scaled;
    public static bool operator <=(Liters a, Liters b) => a.Scaled <= b.Scaled;
    public static bool operator >=(Liters a, Liters b) => a.Scaled >= b.Scaled;
    public static Liters operator +(Liters a, Liters b) => FromScaled(checked(a.Scaled + b.Scaled));
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Electricity readings (same shape for Smc and meter readings): x1000 (morHouses Domain in production).</summary>
public readonly record struct Kwh : IScaled<Kwh>
{
    private const Scaling.Sign Sign = Scaling.Sign.NonNegative;
    private Kwh(long scaled) => Scaled = scaled;

    public static int Decimals => 3;
    public long Scaled { get; }
    public decimal Value => Scaling.ToDecimal(Scaled, Decimals);

    public static Kwh Of(decimal value) => new(Scaling.ToScaled(value, Decimals, Sign, nameof(Kwh)));
    public static Kwh FromScaled(long scaled) { Scaling.RequireSign(scaled, Sign, nameof(Kwh)); return new(scaled); }

    public int CompareTo(Kwh other) => Scaled.CompareTo(other.Scaled);
    public static bool operator <(Kwh a, Kwh b) => a.Scaled < b.Scaled;
    public static bool operator >(Kwh a, Kwh b) => a.Scaled > b.Scaled;
    public static bool operator <=(Kwh a, Kwh b) => a.Scaled <= b.Scaled;
    public static bool operator >=(Kwh a, Kwh b) => a.Scaled >= b.Scaled;
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Interest and tax rate percentages, any sign: x10^4 (morFinance Domain in production).</summary>
public readonly record struct RatePercent : IScaled<RatePercent>
{
    private const Scaling.Sign Sign = Scaling.Sign.Any;
    private RatePercent(long scaled) => Scaled = scaled;

    public static int Decimals => 4;
    public long Scaled { get; }
    public decimal Value => Scaling.ToDecimal(Scaled, Decimals);

    public static RatePercent Of(decimal value) => new(Scaling.ToScaled(value, Decimals, Sign, nameof(RatePercent)));
    public static RatePercent FromScaled(long scaled) => new(scaled);

    public int CompareTo(RatePercent other) => Scaled.CompareTo(other.Scaled);
    public static bool operator <(RatePercent a, RatePercent b) => a.Scaled < b.Scaled;
    public static bool operator >(RatePercent a, RatePercent b) => a.Scaled > b.Scaled;
    public static bool operator <=(RatePercent a, RatePercent b) => a.Scaled <= b.Scaled;
    public static bool operator >=(RatePercent a, RatePercent b) => a.Scaled >= b.Scaled;
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>FX exchange rates, strictly positive: x10^8, the widest scale (morFinance Domain in production).</summary>
public readonly record struct FxRate : IScaled<FxRate>
{
    private const Scaling.Sign Sign = Scaling.Sign.Positive;
    private FxRate(long scaled) => Scaled = scaled;

    public static int Decimals => 8;
    public long Scaled { get; }
    public decimal Value => Scaling.ToDecimal(Scaled, Decimals);

    public static FxRate Of(decimal value) => new(Scaling.ToScaled(value, Decimals, Sign, nameof(FxRate)));
    public static FxRate FromScaled(long scaled) { Scaling.RequireSign(scaled, Sign, nameof(FxRate)); return new(scaled); }

    public int CompareTo(FxRate other) => Scaled.CompareTo(other.Scaled);
    public static bool operator <(FxRate a, FxRate b) => a.Scaled < b.Scaled;
    public static bool operator >(FxRate a, FxRate b) => a.Scaled > b.Scaled;
    public static bool operator <=(FxRate a, FxRate b) => a.Scaled <= b.Scaled;
    public static bool operator >=(FxRate a, FxRate b) => a.Scaled >= b.Scaled;
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// The amount half of <see cref="Money"/>, any sign, x10^4. It exists so the amount column of a two-column Money
/// mapping (design D5) uses the same converter and operators as every other quantity, because EF Core 10 cannot
/// index complex-type members (task 5.1). Meaningful only next to its currency column.
/// </summary>
public readonly record struct MoneyAmount : IScaled<MoneyAmount>
{
    private MoneyAmount(long scaled) => Scaled = scaled;

    public static int Decimals => Money.Decimals;
    public long Scaled { get; }
    public decimal Value => Scaling.ToDecimal(Scaled, Decimals);

    public static MoneyAmount Of(decimal value) => new(Scaling.ToScaled(value, Decimals, Scaling.Sign.Any, nameof(MoneyAmount)));
    public static MoneyAmount FromScaled(long scaled) => new(scaled);

    public int CompareTo(MoneyAmount other) => Scaled.CompareTo(other.Scaled);
    public static bool operator <(MoneyAmount a, MoneyAmount b) => a.Scaled < b.Scaled;
    public static bool operator >(MoneyAmount a, MoneyAmount b) => a.Scaled > b.Scaled;
    public static bool operator <=(MoneyAmount a, MoneyAmount b) => a.Scaled <= b.Scaled;
    public static bool operator >=(MoneyAmount a, MoneyAmount b) => a.Scaled >= b.Scaled;
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// An amount in one currency (design D3/D5): always x10^4 in storage, whatever the currency's exponent.
/// <c>MordWare.Core</c> in production, because vehicles, houses and finance all carry it.
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    public const int Decimals = 4;

    /// <summary>Spike-only exponent table; production takes it from the currency catalog.</summary>
    private static readonly Dictionary<string, int> Exponents = new() { ["EUR"] = 2, ["JPY"] = 0, ["KWD"] = 3 };

    private Money(long scaledAmount, string currency)
    {
        if (currency is not { Length: 3 } || !currency.All(char.IsAsciiLetterUpper))
        {
            throw new ArgumentException("Currency must be a 3-letter ISO 4217 code.", nameof(currency));
        }
        ScaledAmount = scaledAmount;
        Currency = currency;
    }

    public long ScaledAmount { get; }
    public string Currency { get; }
    public decimal Amount => Scaling.ToDecimal(ScaledAmount, Decimals);

    /// <summary>The amount as its own scaled type, for the two-column mapping (design D5).</summary>
    public MoneyAmount Value => MoneyAmount.FromScaled(ScaledAmount);

    public static Money Of(MoneyAmount amount, string currency) => new(amount.Scaled, currency);

    public static Money Of(decimal amount, string currency) =>
        new(Scaling.ToScaled(amount, Decimals, Scaling.Sign.Any, nameof(Money)), currency);

    public static Money FromScaled(long scaledAmount, string currency) => new(scaledAmount, currency);

    public static int ExponentOf(string currency) => Exponents[currency];

    /// <summary>
    /// The amount rounded to the currency's own decimals, for display. The rounding mode is always explicit:
    /// choosing it is morFinance's decision (design, Open Questions).
    /// </summary>
    public decimal RoundForDisplay(MidpointRounding mode) => Math.Round(Amount, ExponentOf(Currency), mode);

    /// <summary>
    /// Price per unit as Money at storage scale: an explicit rounding to 4 decimals, never an implicit one.
    /// </summary>
    public Money Per(decimal quantity, MidpointRounding mode) =>
        Of(Math.Round(Amount / quantity, Decimals, mode), Currency);

    public int CompareTo(Money other)
    {
        RequireSameCurrency(other);
        return ScaledAmount.CompareTo(other.ScaledAmount);
    }

    public static bool operator <(Money a, Money b) => a.CompareTo(b) < 0;
    public static bool operator >(Money a, Money b) => a.CompareTo(b) > 0;
    public static bool operator <=(Money a, Money b) => a.CompareTo(b) <= 0;
    public static bool operator >=(Money a, Money b) => a.CompareTo(b) >= 0;

    public static Money operator +(Money a, Money b)
    {
        a.RequireSameCurrency(b);
        return new(checked(a.ScaledAmount + b.ScaledAmount), a.Currency);
    }

    public override string ToString() => $"{Amount.ToString(CultureInfo.InvariantCulture)} {Currency}";

    private void RequireSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Cannot compare or add {Currency} and {other.Currency}.");
        }
    }
}
