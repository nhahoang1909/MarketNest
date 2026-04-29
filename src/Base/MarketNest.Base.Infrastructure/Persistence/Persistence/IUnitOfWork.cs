using MarketNest.Base.Domain;

namespace MarketNest.Base.Infrastructure;

/// <summary>
///     Single entry-point for persisting changes in a request.
///
///     <para>
///         <b>Lifecycle inside a write request:</b>
///         <list type="number">
///             <item>
///                 The transaction filter (<c>RazorPageTransactionFilter</c> /
///                 <c>TransactionActionFilter</c>) opens a DB transaction before the
///                 handler runs.
///             </item>
///             <item>
///                 The command handler calls <see cref="CommitAsync" /> which:
///                 dispatches <see cref="IPreCommitDomainEvent" />s inside the open
///                 transaction, then calls <c>SaveChangesAsync</c> on all module
///                 DbContexts (<b>does NOT commit the DB transaction</b>).
///             </item>
///             <item>
///                 The filter calls <c>tx.CommitAsync()</c> to finalize the transaction.
///             </item>
///             <item>
///                 The filter calls <see cref="DispatchPostCommitEventsAsync" /> after
///                 a successful commit. Post-commit failures are logged but never roll
///                 back the already-committed transaction.
///             </item>
///         </list>
///     </para>
///
///     <para>
///         <b>Rules for command handlers:</b>
///         Call <see cref="CommitAsync" /> exactly once at the end of the handler.
///         Never call <c>DbContext.SaveChangesAsync()</c> directly.
///         Never call <c>Database.CommitTransactionAsync()</c> — that is the filter's job.
///     </para>
/// </summary>
public interface IUnitOfWork
{
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
    /// </summary>
    Task<int> CommitAsync(CancellationToken ct = default);

    /// <summary>
    ///     Dispatches post-commit domain events after the DB transaction has been
    ///     successfully committed by the filter. Each event is dispatched in a
    ///     try/catch — failures are logged but never throw.
    ///     <b>Call only after the transaction has committed.</b>
    /// </summary>
    Task DispatchPostCommitEventsAsync(CancellationToken ct = default);
}

