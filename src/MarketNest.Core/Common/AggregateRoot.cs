namespace MarketNest.Core.Common;

/// <summary>
/// Aggregate root — only aggregates are persisted via repositories.
/// Domain events are raised inside aggregate methods.
/// </summary>
public abstract class AggregateRoot : Entity<Guid>
{
    protected AggregateRoot() => Id = Guid.NewGuid();
}

public abstract class AggregateRoot<TKey> : Entity<TKey>;
