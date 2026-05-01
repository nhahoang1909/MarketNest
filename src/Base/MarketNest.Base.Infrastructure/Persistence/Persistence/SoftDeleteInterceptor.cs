using MarketNest.Base.Common;
using MarketNest.Base.Domain;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace MarketNest.Base.Infrastructure;

/// <summary>
///     EF Core SaveChanges interceptor that converts physical deletes into soft deletes
///     for entities implementing <see cref="ISoftDeletable"/>.
///     When EF Core detects <c>EntityState.Deleted</c> on an <see cref="ISoftDeletable"/> entity,
///     this interceptor:
///     <list type="number">
///         <item>Changes <c>EntityState.Deleted</c> → <c>EntityState.Modified</c>.</item>
///         <item>Calls <see cref="ISoftDeletable.SoftDelete"/> with <c>UtcNow</c> and the current user ID.</item>
///     </list>
///     As a result, no DELETE SQL is generated — only an UPDATE that sets <c>is_deleted = true</c>.
/// </summary>
/// <remarks>
///     <para>
///         Combine with <c>DddModelBuilderExtensions.ApplySoftDeleteQueryFilters()</c>
///         to also hide soft-deleted rows from normal queries.
///     </para>
///     <para>
///         Register this interceptor on all write-side module DbContexts via
///         <c>DatabaseServiceExtensions.AddModuleDbContext</c>.
///     </para>
/// </remarks>
public sealed class SoftDeleteInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            ConvertDeletesToSoftDeletes(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            ConvertDeletesToSoftDeletes(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    private static void ConvertDeletesToSoftDeletes(DbContext context)
    {
        // Resolve IRuntimeContext from the DbContext's scoped service provider.
        // null when running EF design-time tools (migrations) — handled gracefully below.
        IServiceProvider serviceProvider = ((IInfrastructure<IServiceProvider>)context).Instance;
        IRuntimeContext? runtimeContext = serviceProvider.GetService<IRuntimeContext>();

        Guid? currentUserId = runtimeContext?.CurrentUser.IdOrNull;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        // Materialize the list first — changing EntityState inside foreach would modify the collection.
        var deletedEntries = context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Deleted && e.Entity is ISoftDeletable)
            .ToList();

        foreach (var entry in deletedEntries)
        {
            var softDeletable = (ISoftDeletable)entry.Entity;

            // Prevent actual DELETE SQL — update the row instead.
            entry.State = EntityState.Modified;

            softDeletable.SoftDelete(now, currentUserId);
        }
    }
}

