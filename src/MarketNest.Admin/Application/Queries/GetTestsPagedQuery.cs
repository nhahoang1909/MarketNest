using MarketNest.Core.Common.Cqrs;
using MarketNest.Core.Common.Queries;

namespace MarketNest.Admin.Application;

public record GetTestsPagedQuery : PagedQuery, IQuery<PagedResult<TestDto>>
{
    public string? SearchName { get; init; }
}

