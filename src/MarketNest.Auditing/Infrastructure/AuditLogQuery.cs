using MarketNest.Auditing.Application;
using MarketNest.Auditing.Domain;
using MarketNest.Base.Common;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Auditing.Infrastructure;

/// <summary>
///     Read-side implementation of <see cref="IGetAuditLogsPagedQuery"/>.
///     Uses <see cref="AuditingReadDbContext"/> via the module-local <see cref="BaseQuery{TEntity,TKey}"/>.
/// </summary>
public class AuditLogQuery(AuditingReadDbContext db)
    : BaseQuery<AuditLog, Guid>(db), IGetAuditLogsPagedQuery
{
    public async Task<PagedResult<AuditLogDto>> ExecuteAsync(
        GetAuditLogsQuery query, CancellationToken ct = default)
    {
        IQueryable<AuditLog> q = Db.AuditLogs.AsNoTracking();

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

        int totalCount = await q.CountAsync(ct);

        List<AuditLogDto> items = await q
            .OrderByDescending(x => x.OccurredAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .AsNoTracking()
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
            .ToListAsync(ct);

        return new PagedResult<AuditLogDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }
}

