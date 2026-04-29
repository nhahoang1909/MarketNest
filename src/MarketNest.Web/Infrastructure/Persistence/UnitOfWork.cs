using System.Data;
using MediatR;
using MarketNest.Base.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Scoped Unit of Work for the request lifecycle (HTTP) or explicit job lifecycle (background jobs).
///     Manages domain event dispatching, SaveChanges, and database transactions.
///
///     <para>
///         For HTTP requests: the transaction filter calls BeginTransactionAsync,
///         then the handler executes, then the filter calls CommitAsync, CommitTransactionAsync,
///         DispatchPostCommitEventsAsync, and DisposeAsync.
///     </para>
///
///     <para>
///         For background jobs: the job explicitly calls these methods in try/catch/finally.
///     </para>
/// </summary>
internal sealed partial class UnitOfWork(
    IEnumerable<IModuleDbContext> moduleContexts,
    IPublisher publisher,
    IAppLogger<UnitOfWork> logger)
    : IUnitOfWork
{
    private readonly List<IDomainEvent> _postCommitEvents = [];
    private readonly Dictionary<DbContext, IDbContextTransaction> _transactions = [];

    /// <inheritdoc />
    public async Task BeginTransactionAsync(
        IsolationLevel isolation = IsolationLevel.ReadCommitted,
        CancellationToken ct = default)
    {
        var dbContexts = moduleContexts.Select(m => m.AsDbContext()).ToList();
        foreach (var db in dbContexts)
        {
            var transaction = await db.Database.BeginTransactionAsync(isolation, ct);
            _transactions[db] = transaction;
        }

        Log.InfoTxBegin(logger, dbContexts.Count, isolation);
    }

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
        Log.InfoCommitting(logger);
        var totalRows = 0;
        foreach (IModuleDbContext moduleCtx in moduleContexts)
            totalRows += await moduleCtx.AsDbContext().SaveChangesAsync(ct);

        return totalRows;
    }

    /// <inheritdoc />
    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        foreach (var (_, transaction) in _transactions)
        {
            if (transaction != null)
                await transaction.CommitAsync(ct);
        }

        Log.InfoTxCommitted(logger, _transactions.Count);
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken ct = default)
    {
        _postCommitEvents.Clear();

        foreach (var (_, transaction) in _transactions)
        {
            if (transaction != null)
            {
                try { await transaction.RollbackAsync(ct); }
                catch { /* ignore rollback errors */ }
            }
        }

        Log.InfoTxRolledBack(logger, _transactions.Count);
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

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var (_, transaction) in _transactions)
        {
            if (transaction != null)
                await transaction.DisposeAsync();
        }

        _transactions.Clear();
        _postCommitEvents.Clear();
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.UoWTxBegin, LogLevel.Debug,
            "UoW TX BEGIN — Contexts={ContextCount} Isolation={Isolation}")]
        public static partial void InfoTxBegin(
            ILogger logger, int contextCount, IsolationLevel isolation);

        [LoggerMessage((int)LogEventId.UoWPreCommitDispatching, LogLevel.Debug,
            "Dispatching {Count} pre-commit domain event(s) inside open transaction")]
        public static partial void InfoPreCommitDispatching(ILogger logger, int count);

        [LoggerMessage((int)LogEventId.UoWCommitting, LogLevel.Debug,
            "Calling SaveChangesAsync on all module DbContexts")]
        public static partial void InfoCommitting(ILogger logger);

        [LoggerMessage((int)LogEventId.UoWTxCommitted, LogLevel.Debug,
            "UoW TX COMMITTED — Contexts={ContextCount}")]
        public static partial void InfoTxCommitted(ILogger logger, int contextCount);

        [LoggerMessage((int)LogEventId.UoWTxRolledBack, LogLevel.Warning,
            "UoW TX ROLLED BACK — Contexts={ContextCount}")]
        public static partial void InfoTxRolledBack(ILogger logger, int contextCount);

        [LoggerMessage((int)LogEventId.UoWPostCommitFailed, LogLevel.Warning,
            "Post-commit event dispatch failed for {EventType} — TX already committed, event may be lost")]
        public static partial void WarnPostCommitFailed(ILogger logger, string eventType, Exception ex);
    }
}

