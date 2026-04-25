using MarketNest.Core.Common.Cqrs;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Application;

public record UpdateTestCommand(Guid Id, string Name, TestValueObject Value, IEnumerable<string>? SubTitles = null) : ICommand;

