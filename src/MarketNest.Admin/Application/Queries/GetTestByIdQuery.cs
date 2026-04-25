using MarketNest.Core.Common.Cqrs;
using MarketNest.Core.Common;

namespace MarketNest.Admin.Application;

public record GetTestByIdQuery(Guid Id) : IQuery<TestDto?>;

