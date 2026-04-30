namespace MarketNest.Base.Infrastructure;

/// <summary>
///     Exception thrown when <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>
///     is caught during <see cref="IUnitOfWork.CommitAsync"/>.
///     The transaction filter converts this into a 409 Conflict HTTP response.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    ///     Entity type names that failed the concurrency check.
    /// </summary>
    public IReadOnlyList<string> AffectedEntities { get; init; } = [];
}

