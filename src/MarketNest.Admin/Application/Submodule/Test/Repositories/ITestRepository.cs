using MarketNest.Core.Common.Persistence;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Application;

public interface ITestRepository : IBaseRepository<TestEntity, Guid>
{
    void RemoveSubEntities(IEnumerable<TestSubEntity> entities);
    void AddSubEntity(TestSubEntity entity);
}
