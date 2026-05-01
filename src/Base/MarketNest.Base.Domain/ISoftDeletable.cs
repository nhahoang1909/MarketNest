namespace MarketNest.Base.Domain;

/// <summary>
///     Opt-in interface for entities that support soft deletion — records are never physically removed
///     from the database; instead the <see cref="IsDeleted"/> flag is set to <c>true</c> and
///     a <see cref="DeletedAt"/> / <see cref="DeletedBy"/> audit trail is captured.
/// </summary>
/// <remarks>
///     <b>Behavior:</b>
///     <list type="bullet">
///         <item><c>SoftDeleteInterceptor</c> converts <c>EntityState.Deleted</c> → <c>Modified</c>
///               and calls <see cref="SoftDelete"/> automatically — never use EF <c>Remove()</c>
///               directly on soft-deletable entities.</item>
///         <item><c>DddModelBuilderExtensions.ApplySoftDeleteQueryFilters()</c> registers an EF Core
///               global query filter (<c>WHERE is_deleted = FALSE</c>) on every <see cref="ISoftDeletable"/>
///               entity, so deleted records are invisible to normal queries.</item>
///         <item>To query deleted records, call <c>dbSet.IgnoreQueryFilters()</c>.</item>
///         <item>To restore a soft-deleted record back to active use <see cref="Restore"/>.</item>
///     </list>
///     <b>Example implementation:</b>
///     <code>
///     public class Order : AggregateRoot, ISoftDeletable
///     {
///         public bool IsDeleted { get; private set; }
///         // null until soft-deleted
///         public DateTimeOffset? DeletedAt { get; private set; }
///         // null until soft-deleted; null also for system/background deletions
///         public Guid? DeletedBy { get; private set; }
///
///         void ISoftDeletable.SoftDelete(DateTimeOffset at, Guid? by)
///         {
///             IsDeleted = true;
///             DeletedAt = at;
///             DeletedBy = by;
///         }
///
///         void ISoftDeletable.Restore()
///         {
///             IsDeleted = false;
///             DeletedAt = null;
///             DeletedBy = null;
///         }
///     }
///     </code>
/// </remarks>
public interface ISoftDeletable
{
    /// <summary><c>true</c> when the entity has been soft-deleted and is invisible to normal queries.</summary>
    bool IsDeleted { get; }

    /// <summary>
    ///     UTC timestamp at which the entity was soft-deleted.
    ///     <c>null</c> until <see cref="SoftDelete"/> is called.
    /// </summary>
    DateTimeOffset? DeletedAt { get; }

    /// <summary>
    ///     ID of the user who soft-deleted the entity.
    ///     <c>null</c> until deleted, or <c>null</c> when deleted by a system/background process.
    /// </summary>
    Guid? DeletedBy { get; }

    /// <summary>
    ///     Marks the entity as deleted and records the timestamp and actor.
    ///     Called automatically by <c>SoftDeleteInterceptor</c> — do not call manually.
    /// </summary>
    void SoftDelete(DateTimeOffset at, Guid? by);

    /// <summary>
    ///     Restores a soft-deleted entity to active state, clearing all deletion fields.
    ///     Use via a domain method on the aggregate root (e.g. <c>order.Restore()</c>) rather
    ///     than calling this interface method directly.
    /// </summary>
    void Restore();
}

