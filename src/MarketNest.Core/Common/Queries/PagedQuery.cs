using FluentValidation.Results;

namespace MarketNest.Core.Common.Queries;

/// <summary>
/// Base class for all paged list queries.
/// Provides standard pagination, sorting, and search parameters.
/// </summary>
public abstract record PagedQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public bool SortDesc { get; init; }
    public string? Search { get; init; }

    public int Skip => (Page - 1) * PageSize;

    public virtual IEnumerable<ValidationFailure> Validate()
    {
        if (Page < 1) yield return new("Page", "Page must be >= 1");
        if (PageSize is < 1 or > 100) yield return new("PageSize", "PageSize must be between 1 and 100");
    }
}
