using Microsoft.EntityFrameworkCore;
using MarketNest.Admin.Application;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Infrastructure;

public class TestRepository(AdminDbContext db)
    : BaseRepository<TestEntity, Guid>(db), ITestRepository
{
    public override Task<TestEntity?> GetByKeyAsync(Guid id, CancellationToken ct = default)
        => Db.Tests.Include(x => x.SubEntities).FirstOrDefaultAsync(x => x.Id == id, ct);

    public void RemoveSubEntities(IEnumerable<TestSubEntity> entities)
        => Db.TestSubEntities.RemoveRange(entities);

    public void AddSubEntity(TestSubEntity entity)
        => Db.TestSubEntities.Add(entity);
}
