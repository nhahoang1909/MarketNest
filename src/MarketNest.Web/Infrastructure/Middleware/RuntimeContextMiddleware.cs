using System.Diagnostics;
using System.Security.Claims;
using MarketNest.Base.Common;
using Serilog.Context;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Populates <see cref="HttpRuntimeContext" /> (and therefore <see cref="IRuntimeContext" />)
///     once per request, enriches Serilog <see cref="LogContext" />, tags the OpenTelemetry
///     <see cref="Activity" />, and echoes the <c>X-Correlation-ID</c> response header.
///
///     <para>
///         Must be registered <b>after</b> <c>UseAuthentication()</c> /
///         <c>UseAuthorization()</c> so that <c>HttpContext.User</c> is already populated
///         with JWT claims when we build <see cref="ICurrentUser" />.
///     </para>
/// </summary>
public sealed partial class RuntimeContextMiddleware(
    RequestDelegate next,
    IAppLogger<RuntimeContextMiddleware> logger)
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private const string XForwardedForHeader = "X-Forwarded-For";
    private const int ShortCorrelationLength = 16;
    private const string UnknownIp = "unknown";
    private const string AnonymousUserId = "anonymous";

    public async Task InvokeAsync(HttpContext context)
    {
        var runtimeCtx = context.RequestServices
            .GetRequiredService<HttpRuntimeContext>();

        // 1. CorrelationId: inbound header → ASP.NET TraceIdentifier → auto-generate
        var correlationId = (string?)(context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? context.TraceIdentifier)
            ?? Guid.NewGuid().ToString("N")[..ShortCorrelationLength];

        // 2. Build current user from JWT claims (authentication already ran before this middleware)
        ICurrentUser currentUser = context.User.Identity?.IsAuthenticated is true
            ? new CurrentUser(context.User)
            : AnonymousUser.Instance;

        // 3. Populate the scoped context
        runtimeCtx.CorrelationIdValue = correlationId;
        runtimeCtx.RequestIdValue = context.TraceIdentifier;
        runtimeCtx.StartedAtValue = DateTimeOffset.UtcNow;
        runtimeCtx.CurrentUserValue = currentUser;
        runtimeCtx.ClientIpValue = ResolveClientIp(context.Request);
        runtimeCtx.UserAgentValue = context.Request.Headers.UserAgent.ToString();
        runtimeCtx.HttpMethodValue = context.Request.Method;
        runtimeCtx.RequestPathValue = context.Request.Path.Value;

        // 4. Serilog enrichment — applied to every log line emitted during this request
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("UserId", currentUser.Id?.ToString() ?? AnonymousUserId))
        using (LogContext.PushProperty("UserRole", currentUser.Role ?? AnonymousUserId))
        {
            // 5. OpenTelemetry Activity tags
            var activity = Activity.Current;
            if (activity is not null)
            {
                activity.SetTag("correlation.id", correlationId);
                activity.SetTag("user.id", currentUser.Id?.ToString() ?? AnonymousUserId);
                activity.SetTag("user.role", currentUser.Role ?? AnonymousUserId);
            }

            // 6. Echo the correlation ID back so callers can trace across services
            context.Response.Headers[CorrelationIdHeader] = correlationId;

            Log.InfoRequestStart(
                logger,
                context.Request.Method,
                context.Request.Path.Value ?? "/",
                correlationId,
                currentUser.Id?.ToString() ?? AnonymousUserId);

            await next(context);

            Log.InfoRequestEnd(
                logger,
                context.Request.Method,
                context.Request.Path.Value ?? "/",
                correlationId,
                ((IRuntimeContext)runtimeCtx).ElapsedMs,
                context.Response.StatusCode);
        }
    }

    private static string ResolveClientIp(HttpRequest request)
    {
        // Prefer X-Forwarded-For (first entry = original client, Nginx-aware)
        var forwarded = request.Headers[XForwardedForHeader].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();

        return request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? UnknownIp;
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.RuntimeContextRequestStart, LogLevel.Debug,
            "→ {Method} {Path} — CorrelationId={CorrelationId} UserId={UserId}")]
        public static partial void InfoRequestStart(
            ILogger logger, string method, string path, string correlationId, string userId);

        [LoggerMessage((int)LogEventId.RuntimeContextRequestEnd, LogLevel.Debug,
            "← {Method} {Path} {StatusCode} — CorrelationId={CorrelationId} ElapsedMs={ElapsedMs}")]
        public static partial void InfoRequestEnd(
            ILogger logger, string method, string path, string correlationId, long elapsedMs, int statusCode);
    }
}

