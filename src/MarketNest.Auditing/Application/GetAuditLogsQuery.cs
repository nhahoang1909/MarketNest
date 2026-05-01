using MarketNest.Base.Common;

namespace MarketNest.Auditing.Application;

/// <summary>
///     Paged query for audit logs with optional filters.
///     Used by admin portal to search and review audit history.
/// </summary>
public record GetAuditLogsQuery : PagedQuery, IQuery<PagedResult<AuditLogDto>>
{
    public Guid? ActorId { get; init; }
    public string? EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public string? EventType { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}

public class GetAuditLogsQueryHandler(IGetAuditLogsPagedQuery auditLogQuery)
    : IQueryHandler<GetAuditLogsQuery, PagedResult<AuditLogDto>>
{
    public Task<PagedResult<AuditLogDto>> Handle(GetAuditLogsQuery query, CancellationToken cancellationToken)
        => auditLogQuery.ExecuteAsync(query, cancellationToken);
}
