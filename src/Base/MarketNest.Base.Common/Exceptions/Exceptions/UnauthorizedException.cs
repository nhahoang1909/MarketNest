namespace MarketNest.Base.Common;

/// <summary>
///     Thrown when an operation requires an authenticated user but the current user is anonymous.
///     Raised by <see cref="ICurrentUser.RequireId" /> in write handlers. Not for authorization
///     failures (wrong role) — use the appropriate HTTP 403 pattern for those.
/// </summary>
public sealed class UnauthorizedException(string message = "Authentication is required for this operation.")
    : Exception(message);

