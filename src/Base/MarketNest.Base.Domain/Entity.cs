namespace MarketNest.Base.Domain;

/// <summary>
///     Base entity with strongly-typed ID and domain event support.
/// </summary>
public abstract class Entity<TKey> : IHasDomainEvents, IEquatable<Entity<TKey>>
{
    private readonly List<IDomainEvent> _domainEvents = new();
    public TKey Id { get; protected set; } = default!;
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public bool Equals(Entity<TKey>? other) =>
        other is not null && Id is not null && Id.Equals(other.Id);

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>
    ///     Override to enforce domain invariants that must always hold.
    ///     Called by derived classes at the end of factory methods, constructors,
    ///     and state-changing domain methods to guarantee the entity is never
    ///     left in an inconsistent state.
    ///     <para>
    ///         Throw <see cref="MarketNest.Base.Common.DomainException"/> when an invariant
    ///         is violated — this indicates a programming error, not a user input error
    ///         (use <c>Result&lt;T, Error&gt;</c> for expected business failures).
    ///     </para>
    ///     <example>
    ///         <code>
    ///         protected override void EnsureInvariants()
    ///         {
    ///             if (SalePrice is not null &amp;&amp; SalePrice.Amount >= Price.Amount)
    ///                 throw new DomainException("Sale price must be less than base price.");
    ///         }
    ///         </code>
    ///     </example>
    /// </summary>
    protected virtual void EnsureInvariants() { }

    public override bool Equals(object? obj) => Equals(obj as Entity<TKey>);
    public override int GetHashCode() => Id?.GetHashCode() ?? 0;

    public static bool operator ==(Entity<TKey>? left, Entity<TKey>? right) => Equals(left, right);
    public static bool operator !=(Entity<TKey>? left, Entity<TKey>? right) => !Equals(left, right);
}
