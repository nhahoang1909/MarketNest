namespace MarketNest.Admin.Domain;

public record TestValueObject
{
    public required string Code { get; init; }
    public decimal Amount { get; init; }
}
