using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Application;

/// <summary>Lightweight announcement DTO for public display (banner/hero).</summary>
public record ActiveAnnouncementDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required AnnouncementType Type { get; init; }
    public required string? LinkUrl { get; init; }
    public required string? LinkText { get; init; }
    public required bool IsDismissible { get; init; }
}

