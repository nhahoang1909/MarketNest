using MarketNest.Base.Common;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Immutable <see cref="IRuntimeContext" /> for background jobs.
///     Use the static factory methods instead of DI — jobs have no HTTP context.
///
///     <example>
///         System-triggered timer job (no user):
///         <code>var ctx = BackgroundJobRuntimeContext.ForSystemJob(jobKey);</code>
///     </example>
///
///     <example>
///         Admin-triggered batch job (known admin user ID):
///         <code>var ctx = BackgroundJobRuntimeContext.ForAdminJob(jobKey, adminUserId);</code>
///     </example>
/// </summary>
public sealed class BackgroundJobRuntimeContext : IRuntimeContext
{
    private BackgroundJobRuntimeContext(
        string correlationId,
        ICurrentUser currentUser,
        DateTimeOffset startedAt)
    {
        CorrelationId = correlationId;
        CurrentUser = currentUser;
        StartedAt = startedAt;
    }

    // ── IRuntimeContext ────────────────────────────────────────────

    public string CorrelationId { get; }

    public string RequestId { get; } = Guid.NewGuid().ToString("N")[..8];

    public ICurrentUser CurrentUser { get; }

    public RuntimeExecutionContext Execution => RuntimeExecutionContext.BackgroundJob;

    public DateTimeOffset StartedAt { get; }

    // ── HTTP metadata not available for jobs ──────────────────────

    public string? ClientIp => null;
    public string? UserAgent => null;
    public string? HttpMethod => null;
    public string? RequestPath => null;

    // ── Static factories ──────────────────────────────────────────

    /// <summary>
    ///     Creates a context for a scheduled/timer job that has no human actor (system-triggered).
    ///     <see cref="ICurrentUser.IsAuthenticated" /> is <c>false</c>.
    /// </summary>
    /// <param name="jobKey">Unique job key (e.g. <c>"catalog.variant.expire-sales"</c>).</param>
    public static BackgroundJobRuntimeContext ForSystemJob(string jobKey)
        => new(
            correlationId: $"job:{jobKey}:{Guid.NewGuid():N}",
            currentUser: AnonymousUser.Instance,
            startedAt: DateTimeOffset.UtcNow);

    /// <summary>
    ///     Creates a context for job triggered by an admin user.
    ///     <see cref="ICurrentUser.Id" /> is set to <paramref name="adminUserId" />.
    /// </summary>
    /// <param name="jobKey">Unique job key.</param>
    /// <param name="adminUserId">The ID of the admin who triggered the job.</param>
    public static BackgroundJobRuntimeContext ForAdminJob(string jobKey, Guid adminUserId)
        => new(
            correlationId: $"job:{jobKey}:{Guid.NewGuid():N}",
            currentUser: new SystemJobUser(adminUserId),
            startedAt: DateTimeOffset.UtcNow);
}

