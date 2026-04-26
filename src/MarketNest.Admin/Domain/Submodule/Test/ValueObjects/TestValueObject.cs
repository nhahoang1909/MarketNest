namespace MarketNest.Admin.Domain;

public record TestValueObject
{
    public string Code { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}
