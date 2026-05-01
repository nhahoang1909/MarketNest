using MediatR;

namespace MarketNest.Admin.Application;

public record DeleteAnnouncementCommand(Guid Id) : ICommand<Unit>;

