using MarketNest.Base.Common;
using MarketNest.Base.Domain;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace MarketNest.Base.Infrastructure;

/// <summary>
///     EF Core SaveChanges interceptor that automatically stamps <see cref="ITrackable"/> fields.
///     <list type="bullet">
///         <item>On <c>Added</c>: sets <c>CreatedAt = UtcNow</c> and <c>CreatedBy = currentUserId</c>.</item>
///         <item>On <c>Modified</c>: sets <c>ModifiedAt = UtcNow</c> and <c>ModifiedBy = currentUserId</c>.</item>
///     </list>
///     The current user ID is resolved from <see cref="IRuntimeContext"/> via the DbContext's scoped
///     service provider. If <see cref="IRuntimeContext"/> is unavailable (e.g. migrations), the
///     <c>By</c> fields are left <c>null</c> — this is valid for system/seeded records.
/// </summary>
/// <remarks>
///     Register this interceptor on all write-side module DbContexts alongside
///     <see cref="UpdateTokenInterceptor"/>.
/// </remarks>
public sealed class TrackableInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            StampTrackableEntities(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            StampTrackableEntities(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    private static void StampTrackableEntities(DbContext context)
    {
        // Resolve IRuntimeContext from the DbContext's scoped service provider.
        // null when running EF design-time tools (migrations) — handled gracefully below.
        IServiceProvider serviceProvider = ((IInfrastructure<IServiceProvider>)context).Instance;
        IRuntimeContext? runtimeContext = serviceProvider.GetService<IRuntimeContext>();

        Guid? currentUserId = runtimeContext?.CurrentUser.IdOrNull;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is not ITrackable trackable)
                continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    trackable.StampCreated(now, currentUserId);
                    break;

                case EntityState.Modified:
                    trackable.StampModified(now, currentUserId);
                    break;
            }
        }
    }
}

