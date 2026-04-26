using Microsoft.EntityFrameworkCore;
using MarketNest.Admin.Application;
using MarketNest.Admin.Domain;
using MarketNest.Core.Common.Queries;

namespace MarketNest.Admin.Infrastructure;

public class TestQuery(AdminReadDbContext db)
    : BaseQuery<TestEntity, Guid>(db), ITestQuery, IGetTestsPagedQuery
{
    public override Task<TestEntity?> GetByKeyAsync(Guid id, CancellationToken ct = default)
        => Db.Tests.Include(x => x.SubEntities).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<PagedResult<TestDto>> ExecuteAsync(
        GetTestsPagedQuery request, CancellationToken ct)
    {
        IQueryable<TestEntity> query = Db.Tests;
        if (!string.IsNullOrWhiteSpace(request.SearchName))
            query = query.Where(x => x.Name.Contains(request.SearchName));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.Name)
            .Skip(request.Skip).Take(request.PageSize)
            .Select(x => new TestDto
            {
                Id = x.Id,
                Name = x.Name,
                Value = x.Value,
                SubEntities = x.SubEntities.Select(s => new TestSubDto(s.Id, s.Title)).ToList()
            }).ToListAsync(ct);

        return new PagedResult<TestDto>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = total
        };
    }
}
