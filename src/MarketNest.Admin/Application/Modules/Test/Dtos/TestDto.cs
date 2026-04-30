using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Application;

public record TestSubDto(Guid Id, string Title);

public record TestDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required TestValueObject Value { get; init; }
    public required IReadOnlyList<TestSubDto> SubEntities { get; init; }
}
