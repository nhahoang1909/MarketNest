using MarketNest.Core.Common.Cqrs;
using Microsoft.EntityFrameworkCore;
using MarketNest.Admin.Infrastructure;
using MarketNest.Admin.Application;

namespace MarketNest.Admin.Application;

public class GetTestByIdHandler : IQueryHandler<GetTestByIdQuery, TestDto?>
{
    private readonly AdminDbContext _db;

    public GetTestByIdHandler(AdminDbContext db) => _db = db;

    public async Task<TestDto?> Handle(GetTestByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _db.Tests.Include(x => x.SubEntities).FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity is null)
            return null;

        var dto = new TestDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Value = entity.Value,
            SubEntities = entity.SubEntities.Select(s => new TestSubDto(s.Id, s.Title)).ToList()
        };

        return dto;
    }
}

