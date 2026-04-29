namespace MarketNest.Base.Common;

/// <summary>
///     Represents the identity of the user executing the current request or job.
///     For anonymous requests: <see cref="IsAuthenticated" /> is <c>false</c> and all
///     nullable members are <c>null</c>.
///     Inject via <see cref="IRuntimeContext" /><c>.CurrentUser</c> — never inject directly
///     from the DI container unless you only need the user (use <see cref="IRuntimeContext" />
///     to get everything else too).
/// </summary>
public interface ICurrentUser
{
    /// <summary>Whether the user is authenticated (JWT verified or session active).</summary>
    bool IsAuthenticated { get; }

    /// <summary>The user's unique identifier. <c>null</c> for anonymous requests.</summary>
    Guid? Id { get; }

    /// <summary>The user's display name from the JWT <c>name</c> claim. <c>null</c> if not present.</summary>
    string? Name { get; }

    /// <summary>The user's email from the JWT <c>email</c> claim. <c>null</c> for anonymous requests.</summary>
    string? Email { get; }

    /// <summary>The user's primary role. <c>null</c> for anonymous requests.</summary>
    string? Role { get; }

    /// <summary>
    ///     Returns the user's <see cref="Id" />, throwing <see cref="UnauthorizedException" />
    ///     if the user is anonymous.
    ///     Use in write command handlers where authentication is mandatory.
    /// </summary>
    Guid RequireId();

    /// <summary>Same as <see cref="Id" /> — sugar alias for audit interceptors and logging that must not throw.</summary>
    Guid? IdOrNull => Id;
}

