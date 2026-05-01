using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Application;

/// <summary>Full announcement DTO for admin back-office.</summary>
public record AnnouncementDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required AnnouncementType Type { get; init; }
    public required string? LinkUrl { get; init; }
    public required string? LinkText { get; init; }
    public required DateTimeOffset StartDateUtc { get; init; }
    public required DateTimeOffset EndDateUtc { get; init; }
    public required bool IsPublished { get; init; }
    public required bool IsDismissible { get; init; }
    public required int SortOrder { get; init; }
    public required bool IsCurrentlyActive { get; init; }
}

