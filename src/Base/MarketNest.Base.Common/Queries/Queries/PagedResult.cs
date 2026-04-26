namespace MarketNest.Core.Common.Queries;

/// <summary>
///     Standard paged result envelope for list endpoints.
/// </summary>
public record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrev => Page > 1;
    public bool HasNext => Page < TotalPages;

    public static PagedResult<T> Empty(int page, int pageSize)
        => new() { Items = [], Page = page, PageSize = pageSize, TotalCount = 0 };

    public PagedResult<TOut> Map<TOut>(Func<T, TOut> mapper)
        => new()
        {
            Items = Items.Select(mapper).ToList(),
            Page = Page,
            PageSize = PageSize,
            TotalCount = TotalCount
        };
}
