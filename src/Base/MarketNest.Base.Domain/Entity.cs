namespace MarketNest.Base.Domain;

/// <summary>
///     Base entity with strongly-typed ID and domain event support.
/// </summary>
public abstract class Entity<TKey> : IEquatable<Entity<TKey>>
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public TKey Id { get; protected set; } = default!;
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public bool Equals(Entity<TKey>? other) =>
        other is not null && Id is not null && Id.Equals(other.Id);

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();

    public override bool Equals(object? obj) => Equals(obj as Entity<TKey>);
    public override int GetHashCode() => Id?.GetHashCode() ?? 0;

    public static bool operator ==(Entity<TKey>? left, Entity<TKey>? right) => Equals(left, right);
    public static bool operator !=(Entity<TKey>? left, Entity<TKey>? right) => !Equals(left, right);
}
