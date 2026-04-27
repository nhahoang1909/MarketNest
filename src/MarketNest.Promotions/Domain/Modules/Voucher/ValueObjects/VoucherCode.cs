using System.Text.RegularExpressions;

namespace MarketNest.Promotions.Domain;

public record VoucherCode
{
    private static readonly Regex Pattern = new(@"^[A-Z0-9\-]{6,20}$", RegexOptions.Compiled);

    public string Value { get; }

    public VoucherCode(string value)
    {
        string upper = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!Pattern.IsMatch(upper))
            throw new DomainException("Voucher code must be 6–20 uppercase alphanumeric/hyphen characters.");
        Value = upper;
    }

    public static VoucherCode Generate() =>
        new(Guid.NewGuid().ToString("N")[..10].ToUpperInvariant());

    public override string ToString() => Value;
}
