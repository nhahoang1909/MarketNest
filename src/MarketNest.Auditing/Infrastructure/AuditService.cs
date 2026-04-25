using System.Text.Json;
using MarketNest.Auditing.Domain;
using MarketNest.Core.Contracts;
using MarketNest.Core.Logging;

namespace MarketNest.Auditing.Infrastructure;

/// <summary>
/// Phase 1 implementation: writes audit entries directly to "auditing" schema in shared PostgreSQL.
/// Never throws — audit failures are logged but do not break the main request.
/// </summary>
public class AuditService(AuditingDbContext db, IAppLogger<AuditService> logger) : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task RecordAsync(AuditEntry entry, CancellationToken ct = default)
    {
        try
        {
            var log = AuditLog.Create(
                eventType: entry.EventType,
                actorId: entry.ActorId,
                actorEmail: entry.ActorEmail,
                actorRole: entry.ActorRole,
                entityType: entry.EntityType,
                entityId: entry.EntityId,
                oldValues: Serialize(entry.OldValues),
                newValues: Serialize(entry.NewValues),
                metadata: Serialize(entry.Metadata));

            db.AuditLogs.Add(log);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to record audit log: {EventType} {EntityType}:{EntityId}",
                entry.EventType, entry.EntityType, entry.EntityId);
        }
    }

    public async Task RecordLoginAsync(LoginEntry entry, CancellationToken ct = default)
    {
        try
        {
            var loginEvent = LoginEvent.Create(
                userId: entry.UserId,
                email: entry.Email,
                ipAddress: entry.IpAddress,
                userAgent: entry.UserAgent,
                success: entry.Success,
                failureReason: entry.FailureReason);

            db.LoginEvents.Add(loginEvent);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to record login event: {Email} {Success}",
                entry.Email, entry.Success);
        }
    }

    private static string? Serialize(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, JsonOptions);
}

