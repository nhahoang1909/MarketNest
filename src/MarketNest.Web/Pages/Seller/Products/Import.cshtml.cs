using System.Text.Json;
using MarketNest.Catalog.Application;
using MarketNest.Web.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace MarketNest.Web.Pages.Seller.Products;

/// <summary>
///     Seller Product Import page — three actions:
///     GET  /seller/products/import            → show upload form
///     POST /seller/products/import?handler=Validate → parse file, return preview partial (HTMX)
///     POST /seller/products/import?handler=Execute  → execute import from session (HTMX)
///
///     GET  /seller/products/import/template   → download template .xlsx (separate minimal API endpoint)
/// </summary>
public partial class ImportModel(
    IMediator mediator,
    IExcelService excelService,
    IAntivirusScanner virusScanner,
    IAppLogger<ImportModel> logger) : PageModel
{
    // ── State ─────────────────────────────────────────────────────────────────

    public ExcelImportResult<VariantImportRow>? PreviewResult { get; private set; }
    public string? ErrorMessage { get; private set; }

    /// <summary>
    ///     Opaque session key written to TempData so the Validate → Execute two-step
    ///     shares the import result without re-parsing the file.
    ///     Phase 1: stores serialized row data in TempData (simple, no Redis).
    ///     Phase 2: replace with <c>IImportSessionService</c> (Redis TTL).
    /// </summary>
    public string SessionId { get; private set; } = string.Empty;

    // ── Bind targets ─────────────────────────────────────────────────────────

    [BindProperty] public IFormFile? ImportFile { get; set; }
    [BindProperty] public string? SessionIdInput { get; set; }
    [BindProperty] public string Mode { get; set; } = nameof(VariantImportMode.Upsert);
    [BindProperty] public int TotalValid { get; set; }

    // ── GET ───────────────────────────────────────────────────────────────────

    public void OnGet() { }

    // ── POST: Validate ────────────────────────────────────────────────────────

    /// <summary>
    ///     Parses the uploaded file and returns the import preview partial.
    ///     HTMX swaps the result into #preview-section.
    /// </summary>
    public async Task<IActionResult> OnPostValidateAsync(CancellationToken ct)
    {
        Log.InfoValidateStart(logger);

        if (ImportFile is null || ImportFile.Length == 0)
        {
            return Partial("_ValidationError", "Please select a file to upload.");
        }

        // Layer 1 — file checks
        if (!ImportFile.FileName.EndsWith(ExcelContentTypes.XlsxExtension, StringComparison.OrdinalIgnoreCase))
        {
            Log.WarnInvalidType(logger, ImportFile.FileName);
            return ReturnPreviewError("Only .xlsx files are accepted.");
        }

        if (ImportFile.Length > ExcelUploadRules.MaxFileSizeBytes)
        {
            Log.WarnFileTooLarge(logger, ImportFile.Length);
            return ReturnPreviewError($"File exceeds the {ExcelUploadRules.MaxFileSizeBytes / 1024 / 1024} MB limit.");
        }

        await using var stream = ImportFile.OpenReadStream();

        // Layer 1b — magic bytes
        var magicBuf = new byte[ExcelUploadRules.MagicByteCount];
        var read = await stream.ReadAsync(magicBuf, ct);
        stream.Position = 0;
        if (read < ExcelUploadRules.MagicByteCount || !magicBuf.SequenceEqual(ExcelUploadRules.XlsxMagicBytes))
        {
            Log.WarnInvalidMagicBytes(logger, ImportFile.FileName);
            return ReturnPreviewError("File does not appear to be a valid .xlsx file.");
        }

        // Layer 1c — antivirus
        var scanResult = await virusScanner.ScanAsync(stream, ImportFile.FileName, ct);
        if (!scanResult.IsClean)
        {
            Log.WarnVirusDetected(logger, ImportFile.FileName, scanResult.ThreatName ?? "unknown");
            return ReturnPreviewError("The uploaded file failed the virus scan and was rejected.");
        }

        // Layer 2+3 — parse & row validation
        var template = VariantImportTemplate.Build();
        var importResult = await excelService.ImportWithTemplateAsync(stream, template, ct);

        // Store valid rows in TempData for the Execute step
        var sessionId = Guid.NewGuid().ToString("N");
        TempData[$"import_{sessionId}"] = JsonSerializer.Serialize(importResult.ValidRows);

        Log.InfoValidateComplete(logger, importResult.TotalRows, importResult.SuccessCount, importResult.ErrorCount);

        PreviewResult = importResult;
        SessionId = sessionId;

        return Partial(SharedViewPaths.ImportPreview, new ViewDataDictionary(ViewData)
        {
            ["ValidCount"]   = importResult.SuccessCount,
            ["ErrorCount"]   = importResult.ErrorCount,
            ["SkippedCount"] = importResult.SkippedCount,
            ["TotalRows"]    = importResult.TotalRows,
            ["Errors"]       = importResult.Errors,
            ["ImportType"]   = "variants",
            ["SessionId"]    = sessionId
        });
    }

    // ── POST: Execute ─────────────────────────────────────────────────────────

    /// <summary>
    ///     Executes the import from the previewed rows stored in TempData.
    ///     HTMX swaps the result into #import-result.
    /// </summary>
    public async Task<IActionResult> OnPostExecuteAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(SessionIdInput))
            return Content("<p class='text-danger-600 text-sm'>Session expired. Please re-upload your file.</p>", "text/html");

        var key = $"import_{SessionIdInput}";
        if (TempData[key] is not string json)
            return Content("<p class='text-danger-600 text-sm'>Session not found or expired. Please re-upload your file.</p>", "text/html");

        TempData.Remove(key);

        var validRows = JsonSerializer.Deserialize<List<VariantImportRow>>(json);
        if (validRows is null || validRows.Count == 0)
            return Content("<p class='text-danger-600 text-sm'>No valid rows to import.</p>", "text/html");

        // Parse chosen mode
        if (!Enum.TryParse<VariantImportMode>(Mode, out var importMode))
            importMode = VariantImportMode.Upsert;

        // Get authenticated seller id — placeholder until Identity module is fully wired
        // TODO: use ctx.CurrentUser.RequireId() once Identity is complete
        var sellerId = Guid.Empty;

        // Build a MemoryStream of the serialised rows to pass to the command
        // (BulkImportVariantsCommand currently takes a Stream for Phase 1 parity)
        var rowBytes = System.Text.Encoding.UTF8.GetBytes(json);
        await using var ms = new MemoryStream(rowBytes);

        var command = new BulkImportVariantsCommand(
            sellerId,
            ms,
            "session-replay",
            importMode);

        var result = await mediator.Send(command, ct);

        if (result.IsFailure)
        {
            Log.WarnExecuteFailed(logger, result.Error.Code, result.Error.Message);
            return Content($"<p class='text-danger-600 text-sm'>{result.Error.Message}</p>", "text/html");
        }

        var summary = result.Value;
        Log.InfoExecuteComplete(logger, summary.TotalRows, summary.Created, summary.Updated, summary.Skipped, summary.Errors);

        return Content($"""
            <div class="rounded-xl border border-accent-200 bg-accent-50 p-4 text-sm text-accent-800">
                <p class="font-semibold mb-1">✅ Import complete</p>
                <p>Created: <strong>{summary.Created}</strong> &nbsp;
                   Updated: <strong>{summary.Updated}</strong> &nbsp;
                   Skipped: <strong>{summary.Skipped}</strong> &nbsp;
                   Errors: <strong>{summary.Errors}</strong>
                </p>
            </div>
            """, "text/html");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ContentResult ReturnPreviewError(string message)
    {
        ErrorMessage = message;
        return Content($"""
            <div class="rounded-xl border border-danger-200 bg-danger-50 px-4 py-3 text-sm text-danger-700">
                {System.Web.HttpUtility.HtmlEncode(message)}
            </div>
            """, "text/html");
    }

    private PartialViewResult Partial(string viewName, ViewDataDictionary viewData)
    {
        ViewData.Clear();
        foreach (var kvp in viewData) ViewData[kvp.Key] = kvp.Value;
        return new PartialViewResult
        {
            ViewName = viewName,
            ViewData = new ViewDataDictionary(ViewData)
        };
    }

    // ── Logging ───────────────────────────────────────────────────────────────

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.SellerProductsImportStart, LogLevel.Information,
            "ProductImport — Validate step started")]
        public static partial void InfoValidateStart(ILogger logger);

        [LoggerMessage((int)LogEventId.SellerProductsImportValidated, LogLevel.Information,
            "ProductImport — Validate complete. TotalRows={Total}, Valid={Valid}, Errors={Errors}")]
        public static partial void InfoValidateComplete(ILogger logger, int total, int valid, int errors);

        [LoggerMessage((int)LogEventId.SellerProductsImportExecuted, LogLevel.Information,
            "ProductImport — Execute complete. Total={Total}, Created={Created}, Updated={Updated}, Skipped={Skipped}, Errors={Errors}")]
        public static partial void InfoExecuteComplete(ILogger logger, int total, int created, int updated, int skipped, int errors);

        [LoggerMessage((int)LogEventId.SellerProductsImportError, LogLevel.Warning,
            "ProductImport — Execute failed. Code={Code}, Detail={Detail}")]
        public static partial void WarnExecuteFailed(ILogger logger, string code, string detail);

        [LoggerMessage((int)LogEventId.AntivirusScanInfected, LogLevel.Warning,
            "ProductImport — Virus scan rejected file. FileName={FileName}, Threat={ThreatName}")]
        public static partial void WarnVirusDetected(ILogger logger, string fileName, string threatName);

        [LoggerMessage((int)LogEventId.ExcelImportParseFailed, LogLevel.Warning,
            "ProductImport — Invalid file type. FileName={FileName}")]
        public static partial void WarnInvalidType(ILogger logger, string fileName);

        [LoggerMessage((int)LogEventId.ExcelImportParseFailed + 1, LogLevel.Warning,
            "ProductImport — File too large. Size={Size}")]
        public static partial void WarnFileTooLarge(ILogger logger, long size);

        [LoggerMessage((int)LogEventId.ExcelImportParseFailed + 2, LogLevel.Warning,
            "ProductImport — Invalid magic bytes. FileName={FileName}")]
        public static partial void WarnInvalidMagicBytes(ILogger logger, string fileName);
    }
}

