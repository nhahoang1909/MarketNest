using MarketNest.Base.Common;

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

public class GetLoginEventsQueryHandler(IGetLoginEventsPagedQuery loginEventQuery)
    : IQueryHandler<GetLoginEventsQuery, PagedResult<LoginEventDto>>
{
    public Task<PagedResult<LoginEventDto>> Handle(GetLoginEventsQuery query, CancellationToken cancellationToken)
        => loginEventQuery.ExecuteAsync(query, cancellationToken);
}
