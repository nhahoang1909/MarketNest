using MarketNest.Core.Common.Cqrs;
using MarketNest.Core.Common.Queries;

namespace MarketNest.Admin.Application;

public class GetTestsPagedHandler(IGetTestsPagedQuery query)
    : IQueryHandler<GetTestsPagedQuery, PagedResult<TestDto>>
{
    public Task<PagedResult<TestDto>> Handle(GetTestsPagedQuery request, CancellationToken cancellationToken)
        => query.ExecuteAsync(request, cancellationToken);
}
