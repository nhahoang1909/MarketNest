namespace MarketNest.Base.Domain;

/// <summary>
///     Opt-in interface for entities that require optimistic concurrency control.
///     Entities implementing this interface carry a <see cref="UpdateToken"/> (Guid)
///     that is automatically rotated on every save via <c>UpdateTokenInterceptor</c>.
///     EF Core is configured to use this token as a concurrency token (WHERE clause on UPDATE).
/// </summary>
/// <remarks>
///     <b>Usage:</b>
///     <list type="bullet">
///         <item>Implement on aggregate roots or entities that support concurrent updates.</item>
///         <item>Write-side APIs must include <c>UpdateToken</c> in the command to prove
///               the caller has the latest state.</item>
///         <item>Read-side queries/DTOs must project <c>UpdateToken</c> to the client.</item>
///         <item>Handlers pre-check: <c>if (entity.UpdateToken != command.UpdateToken)</c>
///               → return <c>Error.ConcurrencyConflict(...)</c>.</item>
///     </list>
/// </remarks>
public interface IConcurrencyAware
{
    /// <summary>
    ///     Optimistic concurrency token — rotated to a new Guid on every successful save.
    ///     Clients must send this value back on write operations to prove they have the latest state.
    /// </summary>
    Guid UpdateToken { get; }

    /// <summary>
    ///     Rotates the <see cref="UpdateToken"/> to a new value.
    ///     Called automatically by <c>UpdateTokenInterceptor</c> during SaveChanges — do not call manually.
    /// </summary>
    void RotateUpdateToken();
}

