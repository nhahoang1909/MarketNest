namespace MarketNest.Catalog.Application;

/// <summary>
///     Import mode controls how existing variants are handled during a bulk import.
/// </summary>
public enum VariantImportMode
{
    /// <summary>Create new variants only. Skip rows where SKU already exists on the product.</summary>
    CreateOnly,

    /// <summary>Update existing variants only. Skip rows where SKU does not exist.</summary>
    UpdateOnly,

    /// <summary>Create if absent, update if found. Recommended default.</summary>
    Upsert
}

/// <summary>Result summary returned by <see cref="BulkImportVariantsCommand"/>.</summary>
public record BulkImportVariantsResult(
    int TotalRows,
    int Created,
    int Updated,
    int Skipped,
    int Errors,
    IReadOnlyList<ExcelRowError> RowErrors);

/// <summary>
///     Bulk import product variants from a seller-uploaded .xlsx file.
///     The file is parsed, validated, and applied in the same HTTP request (Phase 1 — synchronous).
///     Phase 2 will move large imports to a background job.
/// </summary>
public record BulkImportVariantsCommand(
    Guid SellerId,
    Stream FileStream,
    string FileName,
    VariantImportMode Mode = VariantImportMode.Upsert)
    : ICommand<BulkImportVariantsResult>;

