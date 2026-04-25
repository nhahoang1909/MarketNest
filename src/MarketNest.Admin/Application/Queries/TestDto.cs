using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Application;

public record TestSubDto(Guid Id, string Title);

public record TestDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public TestValueObject Value { get; init; } = new();
    public IReadOnlyList<TestSubDto> SubEntities { get; init; } = [];
}

