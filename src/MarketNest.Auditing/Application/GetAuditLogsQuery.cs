using MarketNest.Auditing.Domain;
using MarketNest.Auditing.Infrastructure;
using MarketNest.Base.Common;
using Microsoft.EntityFrameworkCore;

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

public class GetAuditLogsQueryHandler(AuditingDbContext db)
    : IQueryHandler<GetAuditLogsQuery, PagedResult<AuditLogDto>>
{
    public async Task<PagedResult<AuditLogDto>> Handle(GetAuditLogsQuery query, CancellationToken cancellationToken)
    {
        IQueryable<AuditLog> q = db.AuditLogs.AsNoTracking().AsQueryable();

        if (query.ActorId.HasValue)
            q = q.Where(x => x.ActorId == query.ActorId);

        if (!string.IsNullOrEmpty(query.EntityType))
            q = q.Where(x => x.EntityType == query.EntityType);

        if (query.EntityId.HasValue)
            q = q.Where(x => x.EntityId == query.EntityId);

        if (!string.IsNullOrEmpty(query.EventType))
            q = q.Where(x => x.EventType == query.EventType);

        if (query.From.HasValue)
            q = q.Where(x => x.OccurredAt >= query.From);

        if (query.To.HasValue)
            q = q.Where(x => x.OccurredAt <= query.To);

        if (!string.IsNullOrEmpty(query.Search))
            q = q.Where(x =>
                (x.ActorEmail != null && x.ActorEmail.Contains(query.Search)) ||
                x.EventType.Contains(query.Search) ||
                (x.EntityType != null && x.EntityType.Contains(query.Search)));

        int totalCount = await q.CountAsync(cancellationToken);

        List<AuditLogDto> items = await q
            .OrderByDescending(x => x.OccurredAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(x => new AuditLogDto
            {
                Id = x.Id,
                EventType = x.EventType,
                ActorId = x.ActorId,
                ActorEmail = x.ActorEmail,
                ActorRole = x.ActorRole,
                EntityType = x.EntityType,
                EntityId = x.EntityId,
                OldValues = x.OldValues,
                NewValues = x.NewValues,
                OccurredAt = x.OccurredAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }
}
