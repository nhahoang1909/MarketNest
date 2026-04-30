namespace MarketNest.Base.Common;

/// <summary>
///     Money value object used across modules (amount + currency). Defined in Base.Common.
/// </summary>
public sealed record Money
{
    public Money(decimal amount, string currency)
    {
        if (amount < 0) throw new ArgumentException("Amount cannot be negative", nameof(amount));
        if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("Currency is required", nameof(currency));

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public decimal Amount { get; init; }
    public string Currency { get; init; }

    public override string ToString() => $"{Amount:F2} {Currency}";
}
