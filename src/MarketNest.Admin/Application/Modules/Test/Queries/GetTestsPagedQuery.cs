using MarketNest.Base.Common;

namespace MarketNest.Admin.Application;

public record GetTestsPagedQuery : PagedQuery, IQuery<PagedResult<TestDto>>
{
    public string? SearchName { get; init; }
}
