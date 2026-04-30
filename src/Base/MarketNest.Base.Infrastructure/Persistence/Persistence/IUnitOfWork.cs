using System.Data;
using MarketNest.Base.Domain;

namespace MarketNest.Base.Infrastructure;

/// <summary>
///     Single entry-point for persisting changes in a request.
///
///     <para>
///         <b>Lifecycle inside a write request (HTTP handler via filter):</b>
///         <list type="number">
///             <item>
///                 The transaction filter (<c>RazorPageTransactionFilter</c> /
///                 <c>TransactionActionFilter</c>) calls <see cref="BeginTransactionAsync" />
///                 on all module DbContexts before the handler runs.
///             </item>
///             <item>
///                 The command handler mutates entities via repositories and returns —
///                 <b>it does NOT call any UoW methods</b>.
///             </item>
///             <item>
///                 The filter calls <see cref="CommitAsync" /> which: dispatches
///                 <see cref="IPreCommitDomainEvent" />s inside the open transaction,
///                 then calls <c>SaveChangesAsync</c> on all module DbContexts
///                 (<b>does NOT commit the DB transaction</b>).
///             </item>
///             <item>
///                 The filter calls <see cref="CommitTransactionAsync" /> to finalize each DB transaction.
///             </item>
///             <item>
///                 The filter calls <see cref="DispatchPostCommitEventsAsync" /> after
///                 a successful commit. Post-commit failures are logged but never roll
///                 back the already-committed transaction.
///             </item>
///             <item>
///                 The filter calls <see cref="DisposeAsync" /> to clean up resources.
///             </item>
///         </list>
///     </para>
///
///     <para>
///         <b>Background jobs (own transaction management):</b>
///         Background jobs run outside the HTTP request pipeline and must manage transactions explicitly.
///         Pattern:
///         <code>
///         try {
///             await uow.BeginTransactionAsync(cancellationToken);
///             // mutate entities via repositories
///             await uow.CommitAsync(cancellationToken); // SaveChanges + pre-commit events
///             await uow.CommitTransactionAsync(cancellationToken); // commit DB transactions
///             await uow.DispatchPostCommitEventsAsync(cancellationToken);
///         } catch (Exception ex) {
///             await uow.RollbackAsync(cancellationToken);
///             throw;
///         } finally {
///             await uow.DisposeAsync();
///         }
///         </code>
///     </para>
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>
    ///     Begins a database transaction on all registered module DbContexts.
    ///     Used by transaction filters (HTTP) and background jobs.
    /// </summary>
    Task BeginTransactionAsync(
        IsolationLevel isolation = IsolationLevel.ReadCommitted,
        CancellationToken ct = default);

    /// <summary>
    ///     Scans all tracked aggregates and partitions their domain events into
    ///     pre-commit (<see cref="IPreCommitDomainEvent" />) and post-commit buckets.
    ///     Returns only the pre-commit events; post-commit events are stored internally
    ///     for <see cref="DispatchPostCommitEventsAsync" />.
    /// </summary>
    IReadOnlyList<IDomainEvent> CollectPreCommitEvents();

    /// <summary>
    ///     Dispatches pre-commit domain events, clears tracked events, then calls
    ///     <c>SaveChangesAsync</c> on every module DbContext.
    ///     <b>Does NOT commit the underlying database transaction.</b>
    ///     The caller is responsible for calling <see cref="CommitTransactionAsync" />
    ///     to finalize the transaction.
    /// </summary>
    Task<int> CommitAsync(CancellationToken ct = default);

    /// <summary>
    ///     Commits all active database transactions (opened by <see cref="BeginTransactionAsync" />).
    ///     Call this only after <see cref="CommitAsync" /> succeeds.
    /// </summary>
    Task CommitTransactionAsync(CancellationToken ct = default);

    /// <summary>
    ///     Rolls back all active database transactions and clears the post-commit event queue.
    ///     Called by error handling in background jobs or filters.
    /// </summary>
    Task RollbackAsync(CancellationToken ct = default);

    /// <summary>
    ///     Dispatches post-commit domain events after the DB transaction has been
    ///     successfully committed via <see cref="CommitTransactionAsync" />.
    ///     Each event is dispatched in a try/catch — failures are logged but never throw.
    ///     <b>Call only after the transaction has committed.</b>
    /// </summary>
    Task DispatchPostCommitEventsAsync(CancellationToken ct = default);
}

