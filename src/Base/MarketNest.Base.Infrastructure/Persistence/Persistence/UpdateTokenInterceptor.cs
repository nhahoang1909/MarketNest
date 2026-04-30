using MarketNest.Base.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MarketNest.Base.Infrastructure;

/// <summary>
///     EF Core SaveChanges interceptor that automatically rotates the <see cref="IConcurrencyAware.UpdateToken"/>
///     for every entity that is Added or Modified. This ensures the concurrency token changes on every write,
///     enabling optimistic concurrency detection.
/// </summary>
/// <remarks>
///     Register this interceptor on all write-side module DbContexts.
///     The token rotation happens in <c>SavingChangesAsync</c> — before the actual SQL is generated —
///     so EF Core includes the NEW token value in the INSERT/UPDATE statement.
/// </remarks>
public sealed class UpdateTokenInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            RotateTokens(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            RotateTokens(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    private static void RotateTokens(DbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is IConcurrencyAware concurrencyAware &&
                entry.State is EntityState.Added or EntityState.Modified)
            {
                concurrencyAware.RotateUpdateToken();
            }
        }
    }
}

