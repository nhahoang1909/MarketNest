namespace MarketNest.Base.Common;

/// <summary>
///     Primary Excel/CSV import-export service contract.
///     All module handlers and pages inject this interface — never the concrete ClosedXML implementation.
///     ADR-037: ClosedXML as primary library; MiniExcel as streaming fallback (Phase 2).
/// </summary>
public interface IExcelService
{
    // ── IMPORT ───────────────────────────────────────────────────────────────

    /// <summary>
    ///     Import rows from an uploaded .xlsx file using a strongly-typed column-mapping template.
    ///     Validates headers against the template before processing any rows (fail-fast on missing required columns).
    /// </summary>
    Task<ExcelImportResult<TRow>> ImportWithTemplateAsync<TRow>(
        Stream fileStream,
        ExcelTemplate<TRow> template,
        CancellationToken ct = default)
        where TRow : class, new();

    // ── EXPORT ───────────────────────────────────────────────────────────────

    /// <summary>
    ///     Export a single-sheet .xlsx workbook from any <see cref="IEnumerable{T}"/>.
    ///     Column definitions control headers, formatting, and ordering.
    /// </summary>
    Task<byte[]> ExportAsync<T>(
        IEnumerable<T> data,
        ExcelExportOptions<T> options,
        CancellationToken ct = default);

    /// <summary>
    ///     Export a multi-sheet .xlsx workbook from multiple typed datasets.
    ///     Each sheet has its own <see cref="ExcelExportOptions{T}"/>.
    /// </summary>
    Task<byte[]> ExportMultiSheetAsync(
        IEnumerable<ExcelSheetDefinition> sheets,
        ExcelWorkbookOptions? workbookOptions = null,
        CancellationToken ct = default);

    // ── TEMPLATE ─────────────────────────────────────────────────────────────

    /// <summary>
    ///     Generate a downloadable .xlsx import template with header row, data-validation dropdowns,
    ///     an example row, and an _Instructions sheet with column descriptions.
    /// </summary>
    Task<byte[]> GenerateImportTemplateAsync<TRow>(
        ExcelTemplate<TRow> template,
        CancellationToken ct = default)
        where TRow : class, new();

    // ── VALIDATION ───────────────────────────────────────────────────────────

    /// <summary>
    ///     Fast header-only validation that checks required columns exist before processing rows.
    ///     Use this as a cheap pre-flight check before committing to a full import.
    /// </summary>
    Task<ExcelHeaderValidationResult> ValidateHeadersAsync<TRow>(
        Stream fileStream,
        ExcelTemplate<TRow> template,
        CancellationToken ct = default)
        where TRow : class, new();
}

