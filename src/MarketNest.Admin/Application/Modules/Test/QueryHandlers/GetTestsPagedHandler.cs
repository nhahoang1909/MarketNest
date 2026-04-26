using MarketNest.Base.Common;

namespace MarketNest.Admin.Application;

public class GetTestsPagedHandler(IGetTestsPagedQuery query)
    : IQueryHandler<GetTestsPagedQuery, PagedResult<TestDto>>
{
    public Task<PagedResult<TestDto>> Handle(GetTestsPagedQuery request, CancellationToken cancellationToken)
        => query.ExecuteAsync(request, cancellationToken);
}
