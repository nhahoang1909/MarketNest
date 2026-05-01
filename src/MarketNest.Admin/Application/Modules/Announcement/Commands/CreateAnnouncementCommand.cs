using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Application;

public record CreateAnnouncementCommand(
    string Title,
    string Message,
    AnnouncementType Type,
    DateTimeOffset StartDateUtc,
    DateTimeOffset EndDateUtc,
    bool IsDismissible,
    int SortOrder,
    string? LinkUrl = null,
    string? LinkText = null) : ICommand<Guid>;

