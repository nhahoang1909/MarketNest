using MarketNest.Base.Common;
using MarketNest.Base.Domain;

namespace MarketNest.Base.Infrastructure;

/// <summary>
///     Helper methods for concurrency token validation in command handlers.
///     Provides early detection of stale data before mutating entities.
/// </summary>
/// <remarks>
///     <b>Usage in a command handler:</b>
///     <code>
///     var entity = await repo.GetByKeyAsync(command.Id, ct);
///     var conflict = ConcurrencyGuard.CheckToken(entity, command.UpdateToken);
///     if (conflict is not null) return conflict;
///     // ... proceed with mutation
///     </code>
///
///     <b>Usage for bulk operations:</b>
///     <code>
///     var pairs = entities.Zip(command.Items, (e, i) => (e as IConcurrencyAware, i.UpdateToken));
///     var conflict = ConcurrencyGuard.CheckTokens(pairs);
///     if (conflict is not null) return conflict;
///     </code>
/// </remarks>
public static class ConcurrencyGuard
{
    /// <summary>
    ///     Checks if the provided <paramref name="clientToken"/> matches the entity's current
    ///     <see cref="IConcurrencyAware.UpdateToken"/>. Returns an error if stale.
    /// </summary>
    /// <param name="entity">The entity to check.</param>
    /// <param name="clientToken">The update token supplied by the client in the command.</param>
    /// <returns>
    ///     <c>null</c> if the token matches (data is fresh);
    ///     an <see cref="Error"/> if the token is stale (data was modified by another user).
    /// </returns>
    public static Error? CheckToken(IConcurrencyAware entity, Guid clientToken)
    {
        if (entity.UpdateToken == clientToken)
            return null;

        var entityName = entity.GetType().Name;
        var entityId = entity is Entity<Guid> e ? e.Id.ToString() : "unknown";

        return Error.ConcurrencyConflict(entityName, entityId);
    }

    /// <summary>
    ///     Checks concurrency tokens for a batch of entities against the provided client tokens.
    ///     Returns <c>null</c> if all tokens match; otherwise returns a bulk conflict error
    ///     listing which items are stale.
    /// </summary>
    /// <remarks>
    ///     For bulk update commands: if ANY item is stale, the entire batch is rejected
    ///     (fail-all strategy). The error message lists all stale IDs so the client knows
    ///     exactly which records need refreshing.
    /// </remarks>
    public static Error? CheckTokens(IEnumerable<(IConcurrencyAware Entity, Guid ClientToken)> items)
    {
        var staleIds = new List<string>();
        string? entityName = null;

        foreach (var (entity, clientToken) in items)
        {
            entityName ??= entity.GetType().Name;

            if (entity.UpdateToken != clientToken)
            {
                var entityId = entity is Entity<Guid> e ? e.Id.ToString() : "unknown";
                staleIds.Add(entityId);
            }
        }

        if (staleIds.Count == 0)
            return null;

        return Error.BulkConcurrencyConflict(entityName ?? "Entity", staleIds);
    }
}

