namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Prevents browsers from caching HTMX partial responses.
///     HTMX sends <c>HX-Request: true</c> header on every AJAX request.
///     Without <c>no-store</c>, the browser may serve a stale partial instead of a full page on back-navigation.
/// </summary>
public sealed class HtmxNoCacheMiddleware(RequestDelegate next)
{
    private const string HtmxRequestHeader = "HX-Request";
    private const string NoCacheControl = "no-store";

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            if (context.Request.Headers.ContainsKey(HtmxRequestHeader))
                context.Response.Headers.CacheControl = NoCacheControl;

            return Task.CompletedTask;
        });

        await next(context);
    }
}

