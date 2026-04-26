using MarketNest.Core.Common.Queries;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Application;

public interface ITestQuery : IBaseQuery<TestEntity, Guid>;
