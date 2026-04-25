using System.Reflection;
using MarketNest.Core.Common;
using MarketNest.Core.Contracts;
using MarketNest.Core.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace MarketNest.Auditing.Infrastructure;

/// <summary>
/// EF Core SaveChanges interceptor that automatically captures changes to entities
/// marked with <see cref="AuditableAttribute"/>. Collects change snapshots before save
/// and writes audit logs after save succeeds.
///
/// Important: This interceptor must NOT be added to <see cref="AuditingDbContext"/> itself
/// to prevent infinite recursion (audit writing triggers another audit).
/// </summary>
public class AuditableInterceptor(IAppLogger<AuditableInterceptor> logger) : SaveChangesInterceptor
{
    private List<PendingAuditEntry>? _pendingEntries;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null or AuditingDbContext)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        _pendingEntries = CollectChanges(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (_pendingEntries is { Count: > 0 } && eventData.Context is not null)
        {
            await FlushAuditEntriesAsync(eventData.Context, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private static List<PendingAuditEntry> CollectChanges(DbContext context)
    {
        var entries = new List<PendingAuditEntry>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            if (entry.Entity.GetType().GetCustomAttribute<AuditableAttribute>() is null)
                continue;

            var entityTypeName = entry.Entity.GetType().Name;
            var eventType = entry.State switch
            {
                EntityState.Added => entityTypeName.ToUpperInvariant() + "_CREATED",
                EntityState.Modified => entityTypeName.ToUpperInvariant() + "_UPDATED",
                EntityState.Deleted => entityTypeName.ToUpperInvariant() + "_DELETED",
                _ => null
            };

            if (eventType is null)
                continue;

            var entityId = entry.Properties
                .FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue as Guid?;

            var oldValues = entry.State == EntityState.Modified
                ? entry.Properties
                    .Where(p => p.IsModified)
                    .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue)
                : entry.State == EntityState.Deleted
                    ? entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue)
                    : null;

            var newValues = entry.State != EntityState.Deleted
                ? entry.Properties
                    .Where(p => p.IsModified || entry.State == EntityState.Added)
                    .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue)
                : null;

            entries.Add(new PendingAuditEntry(
                EventType: eventType,
                EntityType: entityTypeName,
                EntityId: entityId,
                OldValues: oldValues,
                NewValues: newValues));
        }

        return entries;
    }

    private async Task FlushAuditEntriesAsync(DbContext context, CancellationToken cancellationToken)
    {
        try
        {
            var auditService = GetAuditService(context);
            if (auditService is null)
                return;

            foreach (var pending in _pendingEntries!)
            {
                await auditService.RecordAsync(new AuditEntry
                {
                    EventType = pending.EventType,
                    EntityType = pending.EntityType,
                    EntityId = pending.EntityId,
                    OldValues = pending.OldValues,
                    NewValues = pending.NewValues
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to flush audit entries after SaveChanges");
        }
        finally
        {
            _pendingEntries = null;
        }
    }

    private static IAuditService? GetAuditService(DbContext context)
    {
        var serviceProvider = ((IInfrastructure<IServiceProvider>)context).Instance;
        return serviceProvider.GetService<IAuditService>();
    }

    private sealed record PendingAuditEntry(
        string EventType,
        string EntityType,
        Guid? EntityId,
        Dictionary<string, object?>? OldValues,
        Dictionary<string, object?>? NewValues);
}

