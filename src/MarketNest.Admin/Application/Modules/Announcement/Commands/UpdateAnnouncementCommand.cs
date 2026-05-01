using MarketNest.Admin.Domain;
using MediatR;

namespace MarketNest.Admin.Application;

public record UpdateAnnouncementCommand(
    Guid Id,
    string Title,
    string Message,
    AnnouncementType Type,
    DateTimeOffset StartDateUtc,
    DateTimeOffset EndDateUtc,
    bool IsDismissible,
    int SortOrder,
    string? LinkUrl = null,
    string? LinkText = null) : ICommand<Unit>;

