namespace MarketNest.Base.Common;

/// <summary>
///     Defines a typed column mapping used by the Excel import template engine.
///     A <see cref="Setter"/> parses a raw cell string and assigns it to the row DTO.
/// </summary>
public class ExcelColumnDefinition<TRow>
{
    /// <summary>Exact header text expected in the uploaded file (case-sensitive by default).</summary>
    public string Header { get; init; } = string.Empty;

    /// <summary>Human-readable description shown in the generated template's Instructions sheet.</summary>
    public string? Description { get; init; }

    /// <summary>Whether the column must be present and non-empty. Defaults to <c>true</c>.</summary>
    public bool IsRequired { get; init; } = true;

    /// <summary>Example value written into the template's example row (row 2).</summary>
    public string? ExampleValue { get; init; }

    /// <summary>
    ///     Parses <paramref name="raw"/> and assigns the result to <paramref name="row"/>.
    ///     Return <c>Result.Failure&lt;Unit, string&gt;</c> with a user-facing message on parse failure.
    /// </summary>
    public Func<string, TRow, Result<Unit, string>>? Setter { get; init; }

    /// <summary>
    ///     Restricted set of allowed values. When set, the generated template adds a dropdown
    ///     data-validation rule on this column.
    /// </summary>
    public IReadOnlyList<string>? AllowedValues { get; init; }

    /// <summary>Cell format hint used for template and export formatting.</summary>
    public ExcelColumnFormat Format { get; init; }

    /// <summary>Optional custom validation error message surfaced to the user.</summary>
    public string? ValidationMessage { get; init; }
}

/// <summary>
///     Describes the full column-mapping contract for one import type.
///     The importer validates headers against this template before processing any rows.
/// </summary>
public class ExcelTemplate<TRow> where TRow : class, new()
{
    public string TemplateName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    /// <summary>1-based row index of the header row. Defaults to 1.</summary>
    public int HeaderRowIndex { get; init; } = 1;

    /// <summary>1-based row index where data starts. Defaults to 2.</summary>
    public int DataStartRowIndex { get; init; } = 2;

    /// <summary>Worksheet name to read. Defaults to "Data".</summary>
    public string SheetName { get; init; } = "Data";

    /// <summary>When <c>false</c>, columns not defined in the template trigger a header error.</summary>
    public bool AllowExtraColumns { get; init; }

    /// <summary>Stop collecting row errors after the first failure. Defaults to <c>false</c> (collect all).</summary>
    public bool StopOnFirstError { get; init; }

    /// <summary>Maximum number of data rows to process. Rows beyond this limit are silently skipped.</summary>
    public int MaxRows { get; init; } = 5_000;

    public IReadOnlyList<ExcelColumnDefinition<TRow>> Columns { get; init; } = [];
}
