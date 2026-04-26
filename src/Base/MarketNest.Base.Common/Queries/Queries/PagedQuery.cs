using FluentValidation.Results;

namespace MarketNest.Core.Common.Queries;

/// <summary>
///     Base class for all paged list queries.
///     Provides standard pagination, sorting, and search parameters.
/// </summary>
public abstract record PagedQuery
{
    public int Page { get; init; } = DomainConstants.Pagination.MinPage;
    public int PageSize { get; init; } = DomainConstants.Pagination.DefaultPageSize;
    public string? SortBy { get; init; }
    public bool SortDesc { get; init; }
    public string? Search { get; init; }

    public int Skip => (Page - 1) * PageSize;

    public virtual IEnumerable<ValidationFailure> Validate()
    {
        if (Page < DomainConstants.Pagination.MinPage)
            yield return new ValidationFailure(nameof(Page), DomainConstants.ErrorMessages.PageMustBePositive);
        if (PageSize is < DomainConstants.Pagination.MinPageSize or > DomainConstants.Pagination.MaxPageSize)
            yield return new ValidationFailure(nameof(PageSize), DomainConstants.ErrorMessages.PageSizeRange);
    }
}
