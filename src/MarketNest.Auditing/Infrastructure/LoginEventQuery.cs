using MarketNest.Auditing.Application;
using MarketNest.Auditing.Domain;
using MarketNest.Base.Common;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Auditing.Infrastructure;

/// <summary>
///     Read-side implementation of <see cref="IGetLoginEventsPagedQuery"/>.
///     Uses <see cref="AuditingReadDbContext"/> via the module-local <see cref="BaseQuery{TEntity,TKey}"/>.
/// </summary>
public class LoginEventQuery(AuditingReadDbContext db)
    : BaseQuery<LoginEvent, Guid>(db), IGetLoginEventsPagedQuery
{
    public async Task<PagedResult<LoginEventDto>> ExecuteAsync(
        GetLoginEventsQuery query, CancellationToken ct = default)
    {
        IQueryable<LoginEvent> q = Db.LoginEvents.AsNoTracking();

        if (query.UserId.HasValue)
            q = q.Where(x => x.UserId == query.UserId);

        if (!string.IsNullOrEmpty(query.Email))
            q = q.Where(x => x.Email.Contains(query.Email));

        if (!string.IsNullOrEmpty(query.IpAddress))
            q = q.Where(x => x.IpAddress != null && x.IpAddress.Contains(query.IpAddress));

        if (query.Success.HasValue)
            q = q.Where(x => x.Success == query.Success);

        if (query.From.HasValue)
            q = q.Where(x => x.OccurredAt >= query.From);

        if (query.To.HasValue)
            q = q.Where(x => x.OccurredAt <= query.To);

        if (!string.IsNullOrEmpty(query.Search))
            q = q.Where(x =>
                x.Email.Contains(query.Search) ||
                (x.IpAddress != null && x.IpAddress.Contains(query.Search)));

        int totalCount = await q.CountAsync(ct);

        List<LoginEventDto> items = await q
            .OrderByDescending(x => x.OccurredAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .AsNoTracking()
            .Select(x => new LoginEventDto
            {
                Id = x.Id,
                UserId = x.UserId,
                Email = x.Email,
                IpAddress = x.IpAddress,
                UserAgent = x.UserAgent,
                Success = x.Success,
                FailureReason = x.FailureReason,
                OccurredAt = x.OccurredAt
            })
            .ToListAsync(ct);

        return new PagedResult<LoginEventDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }
}

