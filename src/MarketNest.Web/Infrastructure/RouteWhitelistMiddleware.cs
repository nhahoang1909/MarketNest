namespace MarketNest.Web.Infrastructure;

/// <summary>
/// Blocks any request whose path is not in <see cref="AppRoutes.WhitelistedPrefixes"/>.
/// Must be registered AFTER UseStaticFiles so that CSS/JS/images are not affected.
/// </summary>
public sealed class RouteWhitelistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RouteWhitelistMiddleware> _logger;

    public RouteWhitelistMiddleware(RequestDelegate next, ILogger<RouteWhitelistMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string path = context.Request.Path.Value ?? "/";

        if (!AppRoutes.IsAllowed(path))
        {
            _logger.LogWarning("Blocked request to non-whitelisted route: {Path}", path);
            context.Response.Redirect(AppRoutes.NotFound);
            return;
        }

        await _next(context);
    }
}
