using MarketNest.Base.Common;

namespace MarketNest.UnitTests;

/// <summary>
///     Test double for <see cref="IRuntimeContext" />. Use the static builder methods
///     to create pre-configured instances — no DI or HTTP required.
///
///     <example>
///         <code>
///             var ctx = TestRuntimeContext.AsSeller();
///             var handler = new PlaceOrderCommandHandler(repo, ctx, uow);
///         </code>
///     </example>
/// </summary>
public sealed class TestRuntimeContext : IRuntimeContext
{
    // ── Role constants (mirrors AppConstants.Roles) ───────────────────

    private const string RoleAdmin = "admin";
    private const string RoleSeller = "seller";
    private const string RoleBuyer = "buyer";

    // ── IRuntimeContext ───────────────────────────────────────────────

    public string CorrelationId { get; init; } = $"test:{Guid.NewGuid():N}"[..20];
    public string RequestId { get; init; } = $"req:{Guid.NewGuid():N}"[..20];
    public ICurrentUser CurrentUser { get; init; } = TestAnonymousUser.Instance;
    public RuntimeExecutionContext Execution => RuntimeExecutionContext.Test;
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? ClientIp => null;
    public string? UserAgent => null;
    public string? HttpMethod => "POST";
    public string? RequestPath => "/test";

    // ── Builder helpers ───────────────────────────────────────────────

    /// <summary>Anonymous (unauthenticated) runtime context.</summary>
    public static TestRuntimeContext AsAnonymous() => new();

    /// <summary>Authenticated user context with the specified <paramref name="userId" /> and <paramref name="role" />.</summary>
    public static TestRuntimeContext AsUser(Guid userId, string role = RoleBuyer)
        => new() { CurrentUser = new FakeCurrentUser(userId, role) };

    /// <summary>Authenticated admin context.</summary>
    public static TestRuntimeContext AsAdmin(Guid? adminId = null)
        => new() { CurrentUser = new FakeCurrentUser(adminId ?? Guid.NewGuid(), RoleAdmin) };

    /// <summary>Authenticated seller context.</summary>
    public static TestRuntimeContext AsSeller(Guid? sellerId = null)
        => new() { CurrentUser = new FakeCurrentUser(sellerId ?? Guid.NewGuid(), RoleSeller) };

    /// <summary>Authenticated buyer context.</summary>
    public static TestRuntimeContext AsBuyer(Guid? buyerId = null)
        => new() { CurrentUser = new FakeCurrentUser(buyerId ?? Guid.NewGuid(), RoleBuyer) };
}

/// <summary>Anonymous singleton for test contexts.</summary>
internal static class TestAnonymousUser
{
    internal static readonly ICurrentUser Instance = new FakeCurrentUser(null, null);
}

/// <summary>Configurable <see cref="ICurrentUser" /> for unit tests.</summary>
internal sealed class FakeCurrentUser(Guid? id, string? role) : ICurrentUser
{
    public bool IsAuthenticated => id.HasValue;
    public Guid? Id => id;
    public string? Name => id.HasValue ? $"Test User {id.Value:N}" : null;
    public string? Email => id.HasValue ? $"user-{id.Value:N}@test.example" : null;
    public string? Role => role;

    public Guid RequireId()
    {
        if (!id.HasValue)
            throw new UnauthorizedException();
        return id.Value;
    }
}

