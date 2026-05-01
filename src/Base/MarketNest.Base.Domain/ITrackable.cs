namespace MarketNest.Base.Domain;

/// <summary>
///     Opt-in interface for entities that require automatic audit-trail stamping.
///     Entities implementing this interface carry four fields: <see cref="CreatedAt"/>,
///     <see cref="CreatedBy"/>, <see cref="ModifiedAt"/>, and <see cref="ModifiedBy"/>.
///     Fields are set automatically by <c>TrackableInterceptor</c> on every save.
/// </summary>
/// <remarks>
///     <b>Usage:</b>
///     <list type="bullet">
///         <item>Implement on aggregate roots or entities that need a creation/modification trail.</item>
///         <item>Declare properties with <c>{ get; private set; }</c> in the implementing class.</item>
///         <item>Implement <see cref="StampCreated"/> and <see cref="StampModified"/> as explicit
///               interface methods that set the private backing properties — the interceptor calls them;
///               application code never should.</item>
///     </list>
///     <b>Example implementation:</b>
///     <code>
///     public class Product : AggregateRoot, ITrackable
///     {
///         public DateTimeOffset CreatedAt { get; private set; }
///         // null for system/seeded records with no associated user
///         public Guid? CreatedBy { get; private set; }
///         // null until first modification after creation
///         public DateTimeOffset? ModifiedAt { get; private set; }
///         // null until first modification after creation
///         public Guid? ModifiedBy { get; private set; }
///
///         void ITrackable.StampCreated(DateTimeOffset at, Guid? by) { CreatedAt = at; CreatedBy = by; }
///         void ITrackable.StampModified(DateTimeOffset at, Guid? by) { ModifiedAt = at; ModifiedBy = by; }
///     }
///     </code>
/// </remarks>
public interface ITrackable
{
    /// <summary>UTC timestamp when the entity was first persisted.</summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>
    ///     ID of the user who created the entity.
    ///     <c>null</c> for system-generated or seeded records that have no associated user.
    /// </summary>
    Guid? CreatedBy { get; }

    /// <summary>
    ///     UTC timestamp of the most recent change after initial creation.
    ///     <c>null</c> until the entity is first modified.
    /// </summary>
    DateTimeOffset? ModifiedAt { get; }

    /// <summary>
    ///     ID of the user who most recently modified the entity.
    ///     <c>null</c> until the entity is first modified.
    /// </summary>
    Guid? ModifiedBy { get; }

    /// <summary>
    ///     Sets <see cref="CreatedAt"/> and <see cref="CreatedBy"/>.
    ///     Called automatically by <c>TrackableInterceptor</c> on insert — do not call manually.
    /// </summary>
    void StampCreated(DateTimeOffset at, Guid? by);

    /// <summary>
    ///     Sets <see cref="ModifiedAt"/> and <see cref="ModifiedBy"/>.
    ///     Called automatically by <c>TrackableInterceptor</c> on update — do not call manually.
    /// </summary>
    void StampModified(DateTimeOffset at, Guid? by);
}

