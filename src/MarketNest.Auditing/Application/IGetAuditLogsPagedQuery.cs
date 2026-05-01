using MarketNest.Base.Common;

namespace MarketNest.Auditing.Application;

/// <summary>
///     Read-side contract for paged audit log queries.
///     Implemented in Infrastructure by <c>AuditLogQuery</c>.
/// </summary>
public interface IGetAuditLogsPagedQuery
{
    Task<PagedResult<AuditLogDto>> ExecuteAsync(GetAuditLogsQuery query, CancellationToken ct = default);
}

