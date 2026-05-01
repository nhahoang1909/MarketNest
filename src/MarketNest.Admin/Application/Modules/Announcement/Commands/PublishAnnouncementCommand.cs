using MediatR;

namespace MarketNest.Admin.Application;

public record PublishAnnouncementCommand(Guid Id, bool Publish) : ICommand<Unit>;

