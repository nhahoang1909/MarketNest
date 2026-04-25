using MarketNest.Core.Common;

namespace MarketNest.Auditing.Domain;

/// <summary>
/// Append-only login event. Records authentication attempts (success and failure).
/// </summary>
public class LoginEvent : Entity<Guid>
{
    public Guid? UserId { get; private set; }
    public string Email { get; private set; } = null!;
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public bool Success { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private LoginEvent() { }

    public static LoginEvent Create(
        Guid? userId,
        string email,
        string? ipAddress,
        string? userAgent,
        bool success,
        string? failureReason)
    {
        return new LoginEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Email = email,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Success = success,
            FailureReason = failureReason,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }
}

