using MarketNest.Base.Common;

namespace MarketNest.Auditing.Application;

/// <summary>
///     Read-side contract for paged login event queries.
///     Implemented in Infrastructure by <c>LoginEventQuery</c>.
/// </summary>
public interface IGetLoginEventsPagedQuery
{
    Task<PagedResult<LoginEventDto>> ExecuteAsync(GetLoginEventsQuery query, CancellationToken ct = default);
}

