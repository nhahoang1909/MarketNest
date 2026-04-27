using MarketNest.Base.Common;

namespace MarketNest.Admin.Application;

public record GetTestByIdQuery(Guid Id) : IQuery<TestDto?>;
