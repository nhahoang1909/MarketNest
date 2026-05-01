using System.Data;

namespace MarketNest.Base.Utility;

/// <summary>
///     Marks a background job class to be automatically wrapped in a database transaction
///     by <c>BackgroundJobRunner</c> when executing the job.
///
///     <para>
///         Transaction lifecycle (managed by the runner — job does NOT inject <see cref="IUnitOfWork"/>):
///         <list type="number">
///             <item>Runner calls <c>IUnitOfWork.BeginTransactionAsync</c>.</item>
///             <item>Runner calls <c>job.ExecuteAsync</c> — job only mutates entities via repositories.</item>
///             <item>On success: runner calls <c>CommitAsync</c> → <c>CommitTransactionAsync</c> → <c>DispatchPostCommitEventsAsync</c>.</item>
///             <item>On failure: runner calls <c>RollbackAsync</c>, then re-throws the original exception.</item>
///             <item>Always: runner calls <c>DisposeAsync</c>.</item>
///         </list>
///     </para>
///
///     <para>
///         Jobs decorated with this attribute MUST NOT inject or call <c>IUnitOfWork</c> directly
///         — doing so will cause a double-transaction conflict.
///     </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class BackgroundJobTransactionAttribute(
    IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
    int timeoutSeconds = 30) : Attribute
{
    /// <summary>Database transaction isolation level. Defaults to <see cref="IsolationLevel.ReadCommitted"/>.</summary>
    public IsolationLevel IsolationLevel { get; } = isolationLevel;

    /// <summary>
    ///     Maximum seconds before the job's cancellation token fires.
    ///     The transaction is rolled back automatically on timeout.
    ///     Defaults to 30 seconds.
    /// </summary>
    public int TimeoutSeconds { get; } = timeoutSeconds;
}

