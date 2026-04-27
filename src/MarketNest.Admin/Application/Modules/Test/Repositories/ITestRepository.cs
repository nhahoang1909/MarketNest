using MarketNest.Admin.Domain;
using MarketNest.Base.Infrastructure;

namespace MarketNest.Admin.Application;

public interface ITestRepository : IBaseRepository<TestEntity, Guid>
{
    void RemoveSubEntities(IEnumerable<TestSubEntity> entities);
    void AddSubEntity(TestSubEntity entity);
}
