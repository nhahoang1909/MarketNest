namespace MarketNest.Base.Common;

/// <summary>
///     Standard result of an Excel or CSV import operation.
///     Contains all valid rows, all row-level errors, and aggregate counts.
/// </summary>
public record ExcelImportResult<T>
{
    /// <summary>True if at least one row was successfully parsed, regardless of errors.</summary>
    public bool IsSuccess => !Errors.Any() || SuccessCount > 0;

    /// <summary>True if any row-level or header-level errors were collected.</summary>
    public bool HasErrors => Errors.Any();

    /// <summary>Total data rows encountered (valid + error rows combined).</summary>
    public int TotalRows { get; init; }

    /// <summary>Number of rows parsed without errors.</summary>
    public int SuccessCount { get; init; }

    /// <summary>Number of rows skipped (empty rows, over-limit rows, etc.).</summary>
    public int SkippedCount { get; init; }

    /// <summary>Count of rows (or headers) that produced at least one error.</summary>
    public int ErrorCount => Errors.Count;

    /// <summary>Parsed, validated row objects ready for domain processing.</summary>
    public IReadOnlyList<T> ValidRows { get; init; } = [];

    /// <summary>All collected parse/validation errors with row and column context.</summary>
    public IReadOnlyList<ExcelRowError> Errors { get; init; } = [];
}

/// <summary>A single cell-level parse or validation error inside an import batch.</summary>
public record ExcelRowError(
    int RowNumber,
    string Column,
    string Message,
    string? RawValue = null);

/// <summary>
///     Result of the fast header-only validation pass executed before row processing begins.
/// </summary>
public record ExcelHeaderValidationResult(
    bool IsValid,
    IReadOnlyList<string> MissingRequired,
    IReadOnlyList<string> Unexpected);

