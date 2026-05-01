using MarketNest.Base.Common;
using MarketNest.Catalog.Domain;

namespace MarketNest.Catalog.Application;

/// <summary>
///     Excel column name constants for the variant bulk import template.
///     No magic strings anywhere in the import pipeline — use these constants only.
/// </summary>
public static class VariantImportColumns
{
    public const string ProductId    = "Product ID";
    public const string Sku          = "SKU";
    public const string Price        = "Price (USD)";
    public const string ComparePrice = "Compare At Price (USD)";
    public const string Quantity     = "Quantity";
    public const string Status       = "Status";

    /// <summary>Headers array in column order — used by test helpers.</summary>
    public static readonly string[] Headers = [ProductId, Sku, Price, ComparePrice, Quantity, Status];
}

/// <summary>
///     Flat import DTO for a single <see cref="ProductVariant"/> row.
///     Populated by column setters; never maps directly to a domain entity.
/// </summary>
public class VariantImportRow
{
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public int Quantity { get; set; }
    public string Status { get; set; } = EntityStatusNames.Active;
}

/// <summary>
///     Builds the <see cref="ExcelTemplate{TRow}"/> for bulk importing / updating product variants.
///     <para>
///         Import key: <b>SKU</b> (upsert — create if absent, update if found on the same product).
///         Sellers must include the Product ID to scope the import to their store.
///     </para>
/// </summary>
public static class VariantImportTemplate
{
    public static ExcelTemplate<VariantImportRow> Build() => new()
    {
        TemplateName = "Variant Bulk Import",
        Description  = "Bulk create or update product variants. SKU is the unique key per product.",
        SheetName    = "Variants",
        MaxRows      = FieldLimits.ExcelImport.MaxRowsPerBatch,

        Columns =
        [
            new ExcelColumnDefinition<VariantImportRow>
            {
                Header       = VariantImportColumns.ProductId,
                Description  = "UUID of the product this variant belongs to",
                IsRequired   = true,
                ExampleValue = "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                Format       = ExcelColumnFormat.Text,
                Setter       = (raw, row) =>
                {
                    if (!Guid.TryParse(raw, out var id))
                        return Result<Unit, string>.Failure("Product ID must be a valid UUID.");
                    row.ProductId = id;
                    return Result<Unit, string>.Success(Unit.Value);
                }
            },
            new ExcelColumnDefinition<VariantImportRow>
            {
                Header       = VariantImportColumns.Sku,
                Description  = $"Unique SKU code ({FieldLimits.Sku.MinLength}–{FieldLimits.Sku.MaxLength} chars, uppercase)",
                IsRequired   = true,
                ExampleValue = "TSHIRT-RED-M",
                Format       = ExcelColumnFormat.Text,
                Setter       = (raw, row) =>
                {
                    if (raw.Length < FieldLimits.Sku.MinLength || raw.Length > FieldLimits.Sku.MaxLength)
                        return Result<Unit, string>.Failure(
                            $"SKU must be {FieldLimits.Sku.MinLength}–{FieldLimits.Sku.MaxLength} characters.");
                    row.Sku = raw.ToUpperInvariant();
                    return Result<Unit, string>.Success(Unit.Value);
                }
            },
            new ExcelColumnDefinition<VariantImportRow>
            {
                Header       = VariantImportColumns.Price,
                Description  = $"Base price in USD ({FieldLimits.Money.Min:0.00}–{FieldLimits.Money.Max:0.00})",
                IsRequired   = true,
                ExampleValue = "19.99",
                Format       = ExcelColumnFormat.DecimalNumber,
                Setter       = (raw, row) =>
                {
                    if (!decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var price)
                        || price < FieldLimits.Money.Min || price > FieldLimits.Money.Max)
                        return Result<Unit, string>.Failure(
                            $"Price must be a decimal between {FieldLimits.Money.Min} and {FieldLimits.Money.Max}.");
                    row.Price = Math.Round(price, FieldLimits.Money.DecimalPlaces);
                    return Result<Unit, string>.Success(Unit.Value);
                }
            },
            new ExcelColumnDefinition<VariantImportRow>
            {
                Header       = VariantImportColumns.ComparePrice,
                Description  = "Optional compare-at (crossed-out) price in USD",
                IsRequired   = false,
                ExampleValue = "24.99",
                Format       = ExcelColumnFormat.DecimalNumber,
                Setter       = (raw, row) =>
                {
                    if (string.IsNullOrWhiteSpace(raw)) return Result<Unit, string>.Success(Unit.Value);
                    if (!decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var cmp) || cmp < 0)
                        return Result<Unit, string>.Failure("Compare At Price must be a positive decimal.");
                    row.CompareAtPrice = Math.Round(cmp, FieldLimits.Money.DecimalPlaces);
                    return Result<Unit, string>.Success(Unit.Value);
                }
            },
            new ExcelColumnDefinition<VariantImportRow>
            {
                Header       = VariantImportColumns.Quantity,
                Description  = $"Stock quantity on hand (0–{FieldLimits.Quantity.StockMax:N0})",
                IsRequired   = true,
                ExampleValue = "100",
                Format       = ExcelColumnFormat.Number,
                Setter       = (raw, row) =>
                {
                    if (!int.TryParse(raw, out var qty)
                        || qty < FieldLimits.Quantity.StockMin || qty > FieldLimits.Quantity.StockMax)
                        return Result<Unit, string>.Failure(
                            $"Quantity must be an integer from {FieldLimits.Quantity.StockMin} to {FieldLimits.Quantity.StockMax}.");
                    row.Quantity = qty;
                    return Result<Unit, string>.Success(Unit.Value);
                }
            },
            new ExcelColumnDefinition<VariantImportRow>
            {
                Header        = VariantImportColumns.Status,
                Description   = "Variant visibility status",
                IsRequired    = false,
                ExampleValue  = EntityStatusNames.Active,
                AllowedValues = [EntityStatusNames.Active, EntityStatusNames.Draft],
                Setter        = (raw, row) =>
                {
                    row.Status = raw;
                    return Result<Unit, string>.Success(Unit.Value);
                }
            }
        ]
    };
}


