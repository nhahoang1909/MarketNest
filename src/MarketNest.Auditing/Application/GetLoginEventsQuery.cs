using MarketNest.Auditing.Domain;
using MarketNest.Auditing.Infrastructure;
using MarketNest.Base.Common;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Auditing.Application;

/// <summary>
///     Paged query for login events with optional filters.
///     Used by admin portal to review authentication attempts and detect suspicious activity.
/// </summary>
public record GetLoginEventsQuery : PagedQuery, IQuery<PagedResult<LoginEventDto>>
{
    public Guid? UserId { get; init; }
    public string? Email { get; init; }
    public string? IpAddress { get; init; }
    public bool? Success { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}

public class GetLoginEventsQueryHandler(AuditingDbContext db)
    : IQueryHandler<GetLoginEventsQuery, PagedResult<LoginEventDto>>
{
    public async Task<PagedResult<LoginEventDto>> Handle(GetLoginEventsQuery query, CancellationToken cancellationToken)
    {
        IQueryable<LoginEvent> q = db.LoginEvents.AsNoTracking().AsQueryable();

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

        int totalCount = await q.CountAsync(cancellationToken);

        List<LoginEventDto> items = await q
            .OrderByDescending(x => x.OccurredAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
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
            .ToListAsync(cancellationToken);

        return new PagedResult<LoginEventDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }
}
