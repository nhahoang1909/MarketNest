namespace MarketNest.Base.Domain;

/// <summary>
///     Marker interface for domain events that must execute INSIDE the database transaction,
///     BEFORE <c>SaveChanges</c> (i.e., pre-commit side effects that must be atomic with
///     the primary write — e.g., reserving inventory when placing an order).
///
///     Domain events that do NOT implement this interface are treated as <em>post-commit</em>
///     events: they are dispatched AFTER the transaction commits (e.g., sending emails,
///     publishing to outbox). Post-commit failures are logged but never roll back the
///     committed transaction.
/// </summary>
public interface IPreCommitDomainEvent : IDomainEvent
{
}

