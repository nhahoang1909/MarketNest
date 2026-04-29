using System.Security.Claims;
using MarketNest.Base.Common;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     <see cref="ICurrentUser" /> implementation backed by an ASP.NET Core
///     <see cref="ClaimsPrincipal" /> (populated from the JWT after
///     <c>UseAuthentication()</c> runs). Created once per request by
///     <see cref="RuntimeContextMiddleware" />.
/// </summary>
internal sealed class CurrentUser(ClaimsPrincipal principal) : ICurrentUser
{
    public bool IsAuthenticated => principal.Identity?.IsAuthenticated is true;

    public Guid? Id => TryParseGuid(ClaimTypes.NameIdentifier);

    public string? Name => IsAuthenticated
        ? principal.FindFirstValue(ClaimTypes.Name)
        : null;

    public string? Email => IsAuthenticated
        ? principal.FindFirstValue(ClaimTypes.Email)
        : null;

    public string? Role => IsAuthenticated
        ? principal.FindFirstValue(ClaimTypes.Role)
        : null;

    // ── Convenience helpers (Web-layer only, not on the interface) ────

    public bool IsAdmin => Role == AppConstants.Roles.Admin;
    public bool IsSeller => Role == AppConstants.Roles.Seller;
    public bool IsBuyer => Role == AppConstants.Roles.Buyer;

    /// <inheritdoc />
    public Guid RequireId()
    {
        if (!IsAuthenticated || Id is null)
            throw new UnauthorizedException();
        return Id.Value;
    }

    private Guid? TryParseGuid(string claimType)
        => Guid.TryParse(principal.FindFirstValue(claimType), out Guid id) ? id : null;
}

/// <summary>
///     Anonymous user singleton — avoids allocating a <see cref="CurrentUser" /> on every
///     public (non-authenticated) request. All members return <c>null</c> / <c>false</c>.
/// </summary>
internal static class AnonymousUser
{
    internal static readonly ICurrentUser Instance = new CurrentUser(new ClaimsPrincipal());
}

/// <summary>
///     Minimal <see cref="ICurrentUser" /> for admin-triggered background jobs.
///     Has a real <see cref="Id" /> but no email / role (no JWT context available in jobs).
/// </summary>
internal sealed class SystemJobUser(Guid userId) : ICurrentUser
{
    public bool IsAuthenticated => true;
    public Guid? Id => userId;
    public string? Name => null;
    public string? Email => null;
    public string? Role => null;
    public Guid RequireId() => userId;
}

