using MarketNest.Base.Common;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Scoped <see cref="IRuntimeContext" /> implementation for HTTP requests.
///     Registered as Scoped; <see cref="IRuntimeContext" /> is resolved from this same instance.
///     Properties are mutable-internal so <see cref="RuntimeContextMiddleware" /> can
///     populate them once at the start of the request — no other code should mutate them.
/// </summary>
internal sealed class HttpRuntimeContext : IRuntimeContext
{
    // ── Mutable setters (internal — only RuntimeContextMiddleware writes these) ──

    internal string CorrelationIdValue { get; set; } = string.Empty;
    internal string RequestIdValue { get; set; } = string.Empty;
    internal ICurrentUser CurrentUserValue { get; set; } = AnonymousUser.Instance;
    internal DateTimeOffset StartedAtValue { get; set; } = DateTimeOffset.UtcNow;
    internal string? ClientIpValue { get; set; }
    internal string? UserAgentValue { get; set; }
    internal string? HttpMethodValue { get; set; }
    internal string? RequestPathValue { get; set; }

    // ── IRuntimeContext explicit implementation ──────────────────────

    string IRuntimeContext.CorrelationId => CorrelationIdValue;
    string IRuntimeContext.RequestId => RequestIdValue;
    ICurrentUser IRuntimeContext.CurrentUser => CurrentUserValue;
    RuntimeExecutionContext IRuntimeContext.Execution => RuntimeExecutionContext.HttpRequest;
    DateTimeOffset IRuntimeContext.StartedAt => StartedAtValue;
    string? IRuntimeContext.ClientIp => ClientIpValue;
    string? IRuntimeContext.UserAgent => UserAgentValue;
    string? IRuntimeContext.HttpMethod => HttpMethodValue;
    string? IRuntimeContext.RequestPath => RequestPathValue;
}

