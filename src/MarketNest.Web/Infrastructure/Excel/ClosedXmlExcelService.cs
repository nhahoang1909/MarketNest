using ClosedXML.Excel;
using MarketNest.Base.Infrastructure;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     ClosedXML-backed implementation of <see cref="IExcelService"/>.
///     ADR-037: selected over EPPlus (license) and raw OpenXml (low-level verbosity).
///     All parsing, export, and template generation is synchronous inside Task.Run to avoid
///     blocking the thread pool — ClosedXML has no native async API.
/// </summary>
public partial class ClosedXmlExcelProcessor(
    IAppLogger<ClosedXmlExcelProcessor> logger) : IExcelService
{
    // ── IMPORT ───────────────────────────────────────────────────────────────

    public Task<ExcelImportResult<TRow>> ImportWithTemplateAsync<TRow>(
        Stream fileStream,
        ExcelTemplate<TRow> template,
        CancellationToken ct = default)
        where TRow : class, new()
        => Task.Run(() => ImportInternal(fileStream, template, ct), ct);

    private ExcelImportResult<TRow> ImportInternal<TRow>(
        Stream fileStream,
        ExcelTemplate<TRow> template,
        CancellationToken ct)
        where TRow : class, new()
    {
        var errors = new List<ExcelRowError>();
        var validRows = new List<TRow>();

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(fileStream);
        }
        catch (Exception ex)
        {
            Log.WarnParseFailed(logger, ex.Message);
            return new ExcelImportResult<TRow>
            {
                Errors = [new ExcelRowError(0, string.Empty, $"Could not open file: {ex.Message}")]
            };
        }

        using (workbook)
        {
            var ws = workbook.Worksheets.FirstOrDefault(s =>
                         string.Equals(s.Name, template.SheetName, StringComparison.OrdinalIgnoreCase))
                     ?? workbook.Worksheets.FirstOrDefault();

            if (ws is null)
            {
                return new ExcelImportResult<TRow>
                {
                    Errors = [new ExcelRowError(0, string.Empty, "Workbook contains no worksheets.")]
                };
            }

            // ── Header validation ─────────────────────────────────────────
            var colIndex = BuildColumnIndex(ws, template.HeaderRowIndex);
            var missingRequired = template.Columns
                .Where(c => c.IsRequired && !colIndex.ContainsKey(NormalizeHeader(c.Header)))
                .Select(c => c.Header)
                .ToList();

            if (missingRequired.Count > 0)
            {
                Log.WarnMissingHeaders(logger, string.Join(", ", missingRequired));
                return new ExcelImportResult<TRow>
                {
                    Errors = missingRequired
                        .Select(h => new ExcelRowError(template.HeaderRowIndex, h, $"Required column '{h}' is missing."))
                        .ToList()
                };
            }

            // ── Row processing ────────────────────────────────────────────
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? template.DataStartRowIndex - 1;
            var maxDataRow = Math.Min(lastRow, template.MaxRows + template.DataStartRowIndex - 1);
            var skipped = 0;

            for (var rowNum = template.DataStartRowIndex; rowNum <= maxDataRow; rowNum++)
            {
                ct.ThrowIfCancellationRequested();
                var wsRow = ws.Row(rowNum);
                if (IsEmptyRow(wsRow)) { skipped++; continue; }

                var instance = new TRow();
                var rowErrors = new List<ExcelRowError>();

                foreach (var col in template.Columns)
                {
                    var normalizedHeader = NormalizeHeader(col.Header);
                    if (!colIndex.TryGetValue(normalizedHeader, out var colNum)) continue;

                    var raw = wsRow.Cell(colNum).GetString().Trim();

                    if (col.IsRequired && string.IsNullOrWhiteSpace(raw))
                    {
                        rowErrors.Add(new ExcelRowError(rowNum, col.Header,
                            col.ValidationMessage ?? $"'{col.Header}' is required.", raw));
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(raw) && col.Setter is not null)
                    {
                        var setResult = col.Setter(raw, instance);
                        if (setResult.IsFailure)
                            rowErrors.Add(new ExcelRowError(rowNum, col.Header, setResult.Error, raw));
                    }
                }

                if (rowErrors.Count > 0)
                {
                    errors.AddRange(rowErrors);
                }
                else
                {
                    validRows.Add(instance);
                }

                if (template.StopOnFirstError && errors.Count > 0) break;
            }

            Log.InfoImportComplete(logger, typeof(TRow).Name, validRows.Count, errors.Count);

            return new ExcelImportResult<TRow>
            {
                TotalRows = validRows.Count + errors.Select(e => e.RowNumber).Distinct().Count(),
                SuccessCount = validRows.Count,
                SkippedCount = skipped,
                ValidRows = validRows,
                Errors = errors
            };
        }
    }

    // ── EXPORT ───────────────────────────────────────────────────────────────

    public Task<byte[]> ExportAsync<T>(
        IEnumerable<T> data,
        ExcelExportOptions<T> options,
        CancellationToken ct = default)
        => Task.Run(() => ExportInternal(data, options), ct);

    private static byte[] ExportInternal<T>(IEnumerable<T> data, ExcelExportOptions<T> options)
    {
        using var wb = new XLWorkbook();
        AddSheet(wb, data, options);
        return SaveBytes(wb);
    }

    public Task<byte[]> ExportMultiSheetAsync(
        IEnumerable<ExcelSheetDefinition> sheets,
        ExcelWorkbookOptions? workbookOptions = null,
        CancellationToken ct = default)
        => Task.Run(() =>
        {
            using var wb = new XLWorkbook();
            if (workbookOptions?.Author is not null)
                wb.Properties.Author = workbookOptions.Author;
            if (workbookOptions?.Title is not null)
                wb.Properties.Title = workbookOptions.Title;

            foreach (var sheet in sheets)
            {
                // Use reflection to call the strongly-typed AddSheet overload
                var sheetType = sheet.GetType();
                if (!sheetType.IsGenericType) continue;
                var dataType = sheetType.GetGenericArguments()[0];
                var method = typeof(ClosedXmlExcelProcessor)
                    .GetMethod(nameof(AddSheet), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(dataType);
                method.Invoke(null, [wb, sheetType.GetProperty("Data")!.GetValue(sheet),
                    sheetType.GetProperty("Options")!.GetValue(sheet)]);
            }

            return SaveBytes(wb);
        }, ct);

    // ── TEMPLATE GENERATION ───────────────────────────────────────────────────

    public Task<byte[]> GenerateImportTemplateAsync<TRow>(
        ExcelTemplate<TRow> template,
        CancellationToken ct = default)
        where TRow : class, new()
        => Task.Run(() => GenerateTemplateInternal(template), ct);

    private static byte[] GenerateTemplateInternal<TRow>(ExcelTemplate<TRow> template)
        where TRow : class, new()
    {
        using var wb = new XLWorkbook();
        wb.Properties.Title = template.TemplateName;

        // ── Data sheet ──────────────────────────────────────────────────
        var ws = wb.AddWorksheet(template.SheetName);

        // Header row
        for (var c = 0; c < template.Columns.Count; c++)
        {
            var col = template.Columns[c];
            var cell = ws.Cell(template.HeaderRowIndex, c + 1);
            cell.Value = col.Header;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3932");
            cell.Style.Font.FontColor = XLColor.White;

            // Apply data validation dropdown for AllowedValues columns
            if (col.AllowedValues is { Count: > 0 })
            {
                var validation = ws.Column(c + 1).CreateDataValidation();
                validation.List(string.Join(",", col.AllowedValues), true);
                validation.ErrorTitle = "Invalid value";
                validation.ErrorMessage = $"Allowed values: {string.Join(", ", col.AllowedValues)}";
                validation.ShowErrorMessage = true;
            }
        }

        // Example row
        for (var c = 0; c < template.Columns.Count; c++)
        {
            var col = template.Columns[c];
            if (col.ExampleValue is null) continue;
            var cell = ws.Cell(template.DataStartRowIndex, c + 1);
            cell.Value = col.ExampleValue;
            cell.Style.Font.Italic = true;
            cell.Style.Font.FontColor = XLColor.Gray;
        }

        ws.SheetView.FreezeRows(template.HeaderRowIndex);
        ws.Columns().AdjustToContents();

        // ── Instructions sheet ─────────────────────────────────────────
        var instr = wb.AddWorksheet("_Instructions");
        instr.Cell(1, 1).Value = "Column";
        instr.Cell(1, 2).Value = "Description";
        instr.Cell(1, 3).Value = "Required";
        instr.Cell(1, 4).Value = "Example";
        instr.Cell(1, 5).Value = "Allowed Values";

        var instrHeader = instr.Range(1, 1, 1, 5);
        instrHeader.Style.Font.Bold = true;
        instrHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3932");
        instrHeader.Style.Font.FontColor = XLColor.White;

        for (var c = 0; c < template.Columns.Count; c++)
        {
            var col = template.Columns[c];
            instr.Cell(c + 2, 1).Value = col.Header;
            instr.Cell(c + 2, 2).Value = col.Description ?? string.Empty;
            instr.Cell(c + 2, 3).Value = col.IsRequired ? "Yes" : "No";
            instr.Cell(c + 2, 4).Value = col.ExampleValue ?? string.Empty;
            instr.Cell(c + 2, 5).Value =
                col.AllowedValues is { Count: > 0 } ? string.Join(", ", col.AllowedValues) : string.Empty;
        }

        instr.Columns().AdjustToContents();

        return SaveBytes(wb);
    }

    // ── HEADER VALIDATION ─────────────────────────────────────────────────────

    public Task<ExcelHeaderValidationResult> ValidateHeadersAsync<TRow>(
        Stream fileStream,
        ExcelTemplate<TRow> template,
        CancellationToken ct = default)
        where TRow : class, new()
        => Task.Run(() =>
        {
            try
            {
                using var wb = new XLWorkbook(fileStream);
                var ws = wb.Worksheets.FirstOrDefault(s =>
                             string.Equals(s.Name, template.SheetName, StringComparison.OrdinalIgnoreCase))
                         ?? wb.Worksheets.FirstOrDefault();

                if (ws is null)
                    return new ExcelHeaderValidationResult(false,
                        template.Columns.Where(c => c.IsRequired).Select(c => c.Header).ToList(), []);

                var colIndex = BuildColumnIndex(ws, template.HeaderRowIndex);
                var missing = template.Columns
                    .Where(c => c.IsRequired && !colIndex.ContainsKey(NormalizeHeader(c.Header)))
                    .Select(c => c.Header)
                    .ToList();

                var unexpected = template.AllowExtraColumns
                    ? (IReadOnlyList<string>)[]
                    : colIndex.Keys
                        .Where(k => !template.Columns.Any(c => NormalizeHeader(c.Header) == k))
                        .Select(k => k)
                        .ToList();

                return new ExcelHeaderValidationResult(missing.Count == 0, missing, unexpected);
            }
            catch (Exception ex)
            {
                Log.WarnParseFailed(logger, ex.Message);
                return new ExcelHeaderValidationResult(false,
                    template.Columns.Where(c => c.IsRequired).Select(c => c.Header).ToList(), []);
            }
        }, ct);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Dictionary<string, int> BuildColumnIndex(IXLWorksheet ws, int headerRow)
    {
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastCol = ws.Row(headerRow).LastCellUsed()?.Address.ColumnNumber ?? 0;
        for (var c = 1; c <= lastCol; c++)
        {
            var header = ws.Cell(headerRow, c).GetString().Trim();
            if (!string.IsNullOrWhiteSpace(header))
                index.TryAdd(NormalizeHeader(header), c);
        }
        return index;
    }

    private static string NormalizeHeader(string header)
        => header.Trim().ToUpperInvariant();

    private static bool IsEmptyRow(IXLRow row)
        => row.CellsUsed().All(c => string.IsNullOrWhiteSpace(c.GetString()));

    private static void AddSheet<T>(XLWorkbook wb, IEnumerable<T> data, ExcelExportOptions<T> options)
    {
        var ws = wb.AddWorksheet(options.SheetName);
        var rows = data.Take(options.MaxRows).ToList();

        int startRow = 1;

        // Title row
        if (!string.IsNullOrWhiteSpace(options.Title))
        {
            var titleRange = ws.Range(startRow, 1, startRow, options.Columns.Count);
            titleRange.Merge();
            titleRange.FirstCell().Value = options.Title;
            titleRange.Style.Font.Bold = true;
            titleRange.Style.Font.FontSize = 14;
            startRow++;
        }

        // Subtitle row
        if (!string.IsNullOrWhiteSpace(options.Subtitle))
        {
            var subtitleRange = ws.Range(startRow, 1, startRow, options.Columns.Count);
            subtitleRange.Merge();
            subtitleRange.FirstCell().Value = options.Subtitle;
            subtitleRange.Style.Font.Italic = true;
            subtitleRange.Style.Font.FontColor = XLColor.Gray;
            startRow++;
        }

        // Header row
        var headerRow = startRow;
        for (var c = 0; c < options.Columns.Count; c++)
        {
            var col = options.Columns[c];
            var cell = ws.Cell(headerRow, c + 1);
            cell.Value = col.Header;

            if (options.HeaderStyle is not null)
                ApplyStyle(cell.Style, options.HeaderStyle);
            else
            {
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3932");
                cell.Style.Font.FontColor = XLColor.White;
            }

            if (col.Width.HasValue) ws.Column(c + 1).Width = col.Width.Value;
        }

        // Data rows
        for (var r = 0; r < rows.Count; r++)
        {
            var dataRowNum = headerRow + 1 + r;
            var isAlt = options.AlternatingRows && r % 2 == 1;

            for (var c = 0; c < options.Columns.Count; c++)
            {
                var col = options.Columns[c];
                var cell = ws.Cell(dataRowNum, c + 1);
                var value = col.ValueSelector(rows[r]);

                SetCellValue(cell, value, col.Format);

                if (!string.IsNullOrWhiteSpace(col.NumberFormat))
                    cell.Style.NumberFormat.Format = col.NumberFormat;

                if (col.Bold) cell.Style.Font.Bold = true;
                if (isAlt) cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F5F5");

                if (options.DataStyle is not null)
                    ApplyStyle(cell.Style, options.DataStyle);
            }
        }

        // Freeze + AutoFilter
        if (options.FreezeHeaderRow) ws.SheetView.FreezeRows(headerRow);
        if (options.AutoFilter && options.Columns.Count > 0)
        {
            ws.Range(headerRow, 1, headerRow + rows.Count, options.Columns.Count)
              .SetAutoFilter();
        }

        ws.Columns().AdjustToContents();
    }

    private static void SetCellValue(IXLCell cell, object? value, ExcelColumnFormat format)
    {
        if (value is null) { cell.Value = string.Empty; return; }

        switch (format)
        {
            case ExcelColumnFormat.DateTime when value is DateTimeOffset dto:
                cell.Value = dto.LocalDateTime;
                cell.Style.DateFormat.Format = "yyyy-MM-dd HH:mm";
                break;
            case ExcelColumnFormat.Date when value is DateTimeOffset dtDate:
                cell.Value = dtDate.Date;
                cell.Style.DateFormat.Format = "yyyy-MM-dd";
                break;
            case ExcelColumnFormat.Currency or ExcelColumnFormat.DecimalNumber
                when value is decimal dec:
                cell.Value = dec;
                cell.Style.NumberFormat.Format = "#,##0.00";
                break;
            case ExcelColumnFormat.Number when value is int i:
                cell.Value = i;
                break;
            case ExcelColumnFormat.Boolean when value is bool b:
                cell.Value = b ? "Yes" : "No";
                break;
            default:
                cell.Value = value.ToString();
                break;
        }
    }

    private static void ApplyStyle(IXLStyle style, ExcelStyle s)
    {
        if (!string.IsNullOrWhiteSpace(s.BackgroundColor))
            style.Fill.BackgroundColor = XLColor.FromHtml("#" + s.BackgroundColor.TrimStart('#'));
        if (!string.IsNullOrWhiteSpace(s.FontColor))
            style.Font.FontColor = XLColor.FromHtml("#" + s.FontColor.TrimStart('#'));
        if (s.Bold) style.Font.Bold = true;
        if (s.FontSize.HasValue) style.Font.FontSize = s.FontSize.Value;
    }

    private static byte[] SaveBytes(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── Logging ───────────────────────────────────────────────────────────────

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.ExcelImportComplete, LogLevel.Information,
            "Excel import complete — RowType={RowType}, Valid={Valid}, Errors={Errors}")]
        public static partial void InfoImportComplete(ILogger logger, string rowType, int valid, int errors);

        [LoggerMessage((int)LogEventId.ExcelImportHeadersMissing, LogLevel.Warning,
            "Excel import header validation failed — MissingColumns={Columns}")]
        public static partial void WarnMissingHeaders(ILogger logger, string columns);

        [LoggerMessage((int)LogEventId.ExcelImportParseFailed, LogLevel.Warning,
            "Excel file parse failed — Detail={Detail}")]
        public static partial void WarnParseFailed(ILogger logger, string detail);
    }
}

