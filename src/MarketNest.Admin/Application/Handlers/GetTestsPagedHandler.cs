using MarketNest.Core.Common.Cqrs;
using MarketNest.Core.Common.Queries;
using Microsoft.EntityFrameworkCore;
using MarketNest.Admin.Application;
using MarketNest.Admin.Infrastructure;

namespace MarketNest.Admin.Application;

public class GetTestsPagedHandler : IQueryHandler<GetTestsPagedQuery, PagedResult<TestDto>>
{
    private readonly AdminDbContext _db;

    public GetTestsPagedHandler(AdminDbContext db) => _db = db;

    public async Task<PagedResult<TestDto>> Handle(GetTestsPagedQuery request, CancellationToken cancellationToken)
    {
        var query = _db.Tests.AsNoTracking().Include(x => x.SubEntities).AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.SearchName))
            query = query.Where(x => x.Name.Contains(request.SearchName));

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Name).Skip(request.Skip).Take(request.PageSize)
            .Select(x => new TestDto
            {
                Id = x.Id,
                Name = x.Name,
                Value = x.Value,
                SubEntities = x.SubEntities.Select(s => new TestSubDto(s.Id, s.Title)).ToList()
            }).ToListAsync(cancellationToken);

        var result = new PagedResult<TestDto>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = total
        };

        return result;
    }
}

