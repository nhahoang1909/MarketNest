using MarketNest.Base.Common;

namespace MarketNest.Admin.Application;

public interface IGetTestsPagedQuery
{
    Task<PagedResult<TestDto>> ExecuteAsync(GetTestsPagedQuery request, CancellationToken ct);
}
