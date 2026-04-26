using MarketNest.Admin.Domain;
using MarketNest.Base.Common;

namespace MarketNest.Admin.Application;

public record CreateTestCommand(string Name, TestValueObject Value, IEnumerable<string>? SubTitles = null)
    : ICommand<Guid>;
