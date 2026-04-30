namespace MarketNest.Base.Domain;

/// <summary>
///     Non-generic contract for entities that raise domain events.
///     Allows UnitOfWork to scan ChangeTracker without knowing the key type.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}

