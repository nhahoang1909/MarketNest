namespace MarketNest.Base.Common;

/// <summary>
///     Enumerates the execution environment of the current <see cref="IRuntimeContext" />.
///     Used to distinguish HTTP requests, background jobs, and test runs without checking
///     for a null <see cref="Microsoft.AspNetCore.Http.IHttpContextAccessor" />.
/// </summary>
public enum RuntimeExecutionContext
{
    /// <summary>Normal inbound HTTP request (Razor Page or API controller).</summary>
    HttpRequest = 1,

    /// <summary>Timer or batch background job — no <c>HttpContext</c> available.</summary>
    BackgroundJob = 2,

    /// <summary>xUnit / integration test — context created via <c>TestRuntimeContext</c>.</summary>
    Test = 3,
}

/// <summary>
///     Ambient context for the current request or background job. Single injection point that
///     replaces scattered <c>ICurrentUserService</c> + <c>HttpContext.TraceIdentifier</c> calls.
///
///     <para>
///         <b>HTTP requests</b>: Scoped. Populated once by <c>RuntimeContextMiddleware</c> after
///         <c>UseAuthentication()</c> / <c>UseAuthorization()</c> and injected everywhere downstream.
///     </para>
///     <para>
///         <b>Background jobs</b>: Use <c>BackgroundJobRuntimeContext.ForSystemJob()</c> or
///         <c>BackgroundJobRuntimeContext.ForAdminJob()</c> static factories — do not inject via DI.
///     </para>
///     <para>
///         <b>Tests</b>: Use <c>TestRuntimeContext.AsAnonymous()</c>, <c>AsUser()</c>,
///         <c>AsSeller()</c>, <c>AsAdmin()</c> builder helpers — no DI or HTTP needed.
///     </para>
/// </summary>
public interface IRuntimeContext
{
    // ── Tracing ──────────────────────────────────────────────────────

    /// <summary>
    ///     Correlation ID: from the inbound <c>X-Correlation-ID</c> header, or auto-generated.
    ///     Always present — echoed back on the response and pushed to Serilog <c>LogContext</c>.
    ///     Use this in every structured log that should be traceable across services.
    /// </summary>
    string CorrelationId { get; }

    /// <summary>
    ///     ASP.NET Core <c>HttpContext.TraceIdentifier</c> (unique per request).
    ///     Different from <see cref="CorrelationId" />: this is internal; CorrelationId
    ///     is the cross-service trace key.
    /// </summary>
    string RequestId { get; }

    // ── User ─────────────────────────────────────────────────────────

    /// <summary>The authenticated user, or the anonymous user singleton for public requests.</summary>
    ICurrentUser CurrentUser { get; }

    // ── Execution environment ─────────────────────────────────────────

    /// <summary>Whether this context originated from HTTP, a background job, or a test.</summary>
    RuntimeExecutionContext Execution { get; }

    // ── Timing ───────────────────────────────────────────────────────

    /// <summary>UTC timestamp when the request or job started — used to compute <see cref="ElapsedMs" />.</summary>
    DateTimeOffset StartedAt { get; }

    /// <summary>Milliseconds elapsed since <see cref="StartedAt" />.</summary>
    long ElapsedMs => (long)(DateTimeOffset.UtcNow - StartedAt).TotalMilliseconds;

    // ── HTTP metadata (null for background jobs and tests) ────────────

    /// <summary>Client IP address (X-Forwarded-For aware). <c>null</c> outside HTTP.</summary>
    string? ClientIp { get; }

    /// <summary>User-Agent header value. <c>null</c> outside HTTP.</summary>
    string? UserAgent { get; }

    /// <summary>HTTP method (GET, POST, …). <c>null</c> outside HTTP.</summary>
    string? HttpMethod { get; }

    /// <summary>Request path (e.g. <c>/api/v1/catalog/products</c>). <c>null</c> outside HTTP.</summary>
    string? RequestPath { get; }
}

