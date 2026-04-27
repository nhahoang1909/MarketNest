namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Blocks any request whose path is not in <see cref="AppRoutes.WhitelistedPrefixes" />.
///     Must be registered AFTER UseStaticFiles so that CSS/JS/images are not affected.
/// </summary>
public sealed partial class RouteWhitelistMiddleware(
    RequestDelegate next,
    IAppLogger<RouteWhitelistMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        string path = context.Request.Path.Value ?? "/";

        if (!AppRoutes.IsAllowed(path))
        {
            Log.WarnRouteBlocked(logger, path);
            context.Response.Redirect(AppRoutes.NotFound);
            return;
        }

        await next(context);
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.RouteBlocked, LogLevel.Warning,
            "Blocked request to non-whitelisted route: {Path}")]
        public static partial void WarnRouteBlocked(ILogger logger, string path);
    }
}
