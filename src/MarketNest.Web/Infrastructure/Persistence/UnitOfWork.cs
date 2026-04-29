using MediatR;
using MarketNest.Base.Domain;
using MarketNest.Base.Infrastructure;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Scoped Unit of Work for the request lifecycle.
///     Scans all registered module DbContexts for domain events, dispatches
///     pre-commit events inside the open transaction, calls SaveChangesAsync on
///     every module context, and queues post-commit events for later dispatch.
///
///     <para>
///         <b>Transaction ownership</b>: This class does NOT commit the database
///         transaction — the transaction filter is responsible for calling
///         <c>tx.CommitAsync()</c> after <see cref="CommitAsync" /> returns.
///     </para>
/// </summary>
internal sealed partial class UnitOfWork(
    IEnumerable<IModuleDbContext> moduleContexts,
    IPublisher publisher,
    IAppLogger<UnitOfWork> logger)
    : IUnitOfWork
{
    private readonly List<IDomainEvent> _postCommitEvents = [];

    /// <inheritdoc />
    public IReadOnlyList<IDomainEvent> CollectPreCommitEvents()
    {
        var allEvents = moduleContexts
            .Select(m => m.AsDbContext())
            .SelectMany(db => db.ChangeTracker
                .Entries()
                .Where(e => e.Entity is IHasDomainEvents)
                .Select(e => (IHasDomainEvents)e.Entity)
                .SelectMany(aggregate => aggregate.DomainEvents))
            .ToList();

        // Partition: post-commit (non-pre-commit) events saved for later dispatch
        _postCommitEvents.AddRange(allEvents.Where(e => e is not IPreCommitDomainEvent));

        // Return only pre-commit events (dispatched INSIDE the open transaction)
        return allEvents.Where(e => e is IPreCommitDomainEvent).ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<int> CommitAsync(CancellationToken ct = default)
    {
        // 1. Collect and separate events
        IReadOnlyList<IDomainEvent> preCommitEvents = CollectPreCommitEvents();

        // 2. Dispatch pre-commit events (inside the open transaction, before SaveChanges)
        Log.InfoPreCommitDispatching(logger, preCommitEvents.Count);
        foreach (IDomainEvent domainEvent in preCommitEvents)
            await publisher.Publish(domainEvent, ct);

        // 3. Clear domain events from all tracked aggregates (prevent double-dispatch)
        foreach (IModuleDbContext moduleCtx in moduleContexts)
        {
            foreach (var entry in moduleCtx.AsDbContext().ChangeTracker.Entries()
                         .Where(e => e.Entity is IHasDomainEvents)
                         .Select(e => (IHasDomainEvents)e.Entity))
            {
                entry.ClearDomainEvents();
            }
        }

        // 4. SaveChanges on all module contexts — persists within the open DB transaction
        //    (does NOT commit the transaction — the filter does that)
        Log.InfoCommitting(logger);
        var totalRows = 0;
        foreach (IModuleDbContext moduleCtx in moduleContexts)
            totalRows += await moduleCtx.AsDbContext().SaveChangesAsync(ct);

        return totalRows;
    }

    /// <inheritdoc />
    public async Task DispatchPostCommitEventsAsync(CancellationToken ct = default)
    {
        foreach (IDomainEvent domainEvent in _postCommitEvents)
        {
            try
            {
                await publisher.Publish(domainEvent, ct);
            }
            catch (Exception ex)
            {
                // Post-commit failure NEVER rolls back the already-committed transaction.
                // Phase 3: replace with Outbox pattern for guaranteed delivery.
                Log.WarnPostCommitFailed(logger, domainEvent.GetType().Name, ex);
            }
        }

        _postCommitEvents.Clear();
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.UoWPreCommitDispatching, LogLevel.Debug,
            "Dispatching {Count} pre-commit domain event(s) inside open transaction")]
        public static partial void InfoPreCommitDispatching(ILogger logger, int count);

        [LoggerMessage((int)LogEventId.UoWCommitting, LogLevel.Debug,
            "Calling SaveChangesAsync on all module DbContexts")]
        public static partial void InfoCommitting(ILogger logger);

        [LoggerMessage((int)LogEventId.UoWPostCommitFailed, LogLevel.Warning,
            "Post-commit event dispatch failed for {EventType} — TX already committed, event may be lost")]
        public static partial void WarnPostCommitFailed(ILogger logger, string eventType, Exception ex);
    }
}

