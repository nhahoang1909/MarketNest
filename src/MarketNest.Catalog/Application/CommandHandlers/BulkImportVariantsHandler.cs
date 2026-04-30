using MarketNest.Catalog.Domain;

namespace MarketNest.Catalog.Application;

/// <summary>
///     Handles <see cref="BulkImportVariantsCommand"/>:
///     validates file, parses rows, upserts <see cref="ProductVariant"/> entities.
///     UnitOfWork is committed automatically by the transaction filter — no SaveChanges here.
/// </summary>
[Audited("CATALOG.BULK_IMPORT_VARIANTS")]
public partial class BulkImportVariantsHandler(
    IExcelService excel,
    IAntivirusScanner virusScanner,
    IVariantRepository variants,
    IAppLogger<BulkImportVariantsHandler> logger)
    : ICommandHandler<BulkImportVariantsCommand, BulkImportVariantsResult>
{
    public async Task<Result<BulkImportVariantsResult, Error>> Handle(
        BulkImportVariantsCommand command, CancellationToken cancellationToken)
    {
        Log.InfoStart(logger, command.SellerId, command.FileName, command.Mode.ToString());

        // ── Layer 1: File-level validation ────────────────────────────────
        var fileValidation = ValidateFile(command.FileStream, command.FileName);
        if (fileValidation is not null)
            return Result<BulkImportVariantsResult, Error>.Failure(fileValidation);

        // ── Layer 1b: Antivirus scan ──────────────────────────────────────
        var scanResult = await virusScanner.ScanAsync(
            command.FileStream, command.FileName, cancellationToken);
        if (!scanResult.IsClean)
        {
            Log.WarnVirusDetected(logger, command.FileName, scanResult.ThreatName ?? "unknown");
            return Result<BulkImportVariantsResult, Error>.Failure(ExcelErrors.VirusScanFailed);
        }

        // ── Layer 2+3: Parse + row validation ─────────────────────────────
        var template = VariantImportTemplate.Build();
        var importResult = await excel.ImportWithTemplateAsync(
            command.FileStream, template, cancellationToken);

        if (importResult.Errors.Count > 0 && importResult.ValidRows.Count == 0)
        {
            Log.WarnNoValidRows(logger, importResult.ErrorCount);
            return Result<BulkImportVariantsResult, Error>.Failure(ExcelErrors.NoValidRows);
        }

        // ── Layer 4: Domain processing ────────────────────────────────────
        var created = 0; var updated = 0; var skipped = 0;
        var additionalErrors = new List<ExcelRowError>();

        foreach (var row in importResult.ValidRows)
        {
            // Phase 1: FindBySku returns null — all rows create new variants.
            // Phase 2: add IVariantRepository.FindBySkuAsync(productId, sku, ct).
            var existing = await FindBySkuAsync(row.ProductId, row.Sku, cancellationToken);

            switch (command.Mode)
            {
                case VariantImportMode.UpdateOnly when existing is null:
                    skipped++;
                    continue;
                case VariantImportMode.CreateOnly when existing is not null:
                    skipped++;
                    continue;
            }

            if (existing is null)
            {
                var currency = DomainConstants.Currencies.Default;
                var price = new Money(row.Price, currency);
                Money? compareAt = row.CompareAtPrice.HasValue
                    ? new Money(row.CompareAtPrice.Value, currency)
                    : null;

                var variant = ProductVariant.Create(row.ProductId, row.Sku, price,
                    row.Quantity, compareAt);
                variants.Add(variant);
                created++;
            }
            else
            {
                // TODO: add ProductVariant.UpdatePrice(Money) and UpdateStock(int)
                // domain methods in Phase 2 to enable full update path.
                skipped++;
            }
        }

        Log.InfoComplete(logger, command.SellerId, importResult.TotalRows,
            created, updated, skipped, additionalErrors.Count + importResult.ErrorCount);

        return Result<BulkImportVariantsResult, Error>.Success(
            new BulkImportVariantsResult(
                importResult.TotalRows,
                created,
                updated,
                skipped,
                importResult.ErrorCount + additionalErrors.Count,
                [.. importResult.Errors, .. additionalErrors]));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Error? ValidateFile(Stream fileStream, string fileName)
    {
        if (!fileName.EndsWith(ExcelContentTypes.XlsxExtension, StringComparison.OrdinalIgnoreCase))
            return ExcelErrors.InvalidFileType;

        if (fileStream.Length > ExcelUploadRules.MaxFileSizeBytes)
            return ExcelErrors.FileTooLarge(fileStream.Length);

        if (!HasXlsxMagicBytes(fileStream))
            return ExcelErrors.InvalidFileMagicBytes;

        return null;
    }

    private static bool HasXlsxMagicBytes(Stream stream)
    {
        if (!stream.CanSeek) return true; // cannot verify — trust extension check
        var buf = new byte[ExcelUploadRules.MagicByteCount];
        stream.Position = 0;
        var read = stream.Read(buf, 0, buf.Length);
        stream.Position = 0;
        if (read < ExcelUploadRules.MagicByteCount) return false;
        return buf.SequenceEqual(ExcelUploadRules.XlsxMagicBytes);
    }

    /// <remarks>
    ///     Phase 1: always returns null (creates all rows as new variants).
    ///     Phase 2: add <c>IVariantRepository.FindBySkuAsync(productId, sku, ct)</c>.
    /// </remarks>
    private static Task<ProductVariant?> FindBySkuAsync(
        Guid productId, string sku, CancellationToken cancellationToken)
    {
        _ = (productId, sku, cancellationToken); // suppress unused parameter warnings
        return Task.FromResult<ProductVariant?>(null);
    }

    // ── Logging ───────────────────────────────────────────────────────────────

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.CatalogBulkImportStart, LogLevel.Information,
            "BulkImportVariants Start — SellerId={SellerId}, File={FileName}, Mode={Mode}")]
        public static partial void InfoStart(ILogger logger, Guid sellerId, string fileName, string mode);

        [LoggerMessage((int)LogEventId.CatalogBulkImportSuccess, LogLevel.Information,
            "BulkImportVariants Complete — SellerId={SellerId}, Total={Total}, Created={Created}, Updated={Updated}, Skipped={Skipped}, Errors={Errors}")]
        public static partial void InfoComplete(ILogger logger, Guid sellerId, int total,
            int created, int updated, int skipped, int errors);

        [LoggerMessage((int)LogEventId.CatalogBulkImportFailed, LogLevel.Warning,
            "BulkImportVariants — no valid rows after parse. ErrorCount={ErrorCount}")]
        public static partial void WarnNoValidRows(ILogger logger, int errorCount);

        [LoggerMessage((int)LogEventId.AntivirusScanInfected, LogLevel.Warning,
            "Antivirus scan rejected file — FileName={FileName}, Threat={ThreatName}")]
        public static partial void WarnVirusDetected(ILogger logger, string fileName, string threatName);
    }
}
