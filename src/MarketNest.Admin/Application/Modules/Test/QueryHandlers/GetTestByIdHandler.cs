using MarketNest.Admin.Domain;
using MarketNest.Base.Common;

namespace MarketNest.Admin.Application;

public class GetTestByIdHandler(ITestQuery query) : IQueryHandler<GetTestByIdQuery, TestDto?>
{
    public async Task<TestDto?> Handle(GetTestByIdQuery request, CancellationToken cancellationToken)
    {
        TestEntity? entity = await query.GetByKeyAsync(request.Id, cancellationToken);
        if (entity is null) return null;

        return new TestDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Value = entity.Value,
            SubEntities = entity.SubEntities.Select(s => new TestSubDto(s.Id, s.Title)).ToList()
        };
    }
}
