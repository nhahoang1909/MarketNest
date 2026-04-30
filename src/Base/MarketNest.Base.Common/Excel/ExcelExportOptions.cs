namespace MarketNest.Base.Common;

/// <summary>Column definition for a single field in an export sheet.</summary>
public class ExcelColumnExport<T>
{
    public string Header { get; init; } = string.Empty;
    public Func<T, object?> ValueSelector { get; init; } = _ => null;
    public ExcelColumnFormat Format { get; init; }

    /// <summary>Column width in characters. <c>null</c> = auto-fit.</summary>
    public int? Width { get; init; }

    public bool Bold { get; init; }

    /// <summary>Excel number format string, e.g. <c>#,##0.00</c>. Overrides <see cref="Format" />.</summary>
    public string? NumberFormat { get; init; }
}

/// <summary>Style descriptor for header or data rows in an exported sheet.</summary>
public class ExcelStyle
{
    /// <summary>Background fill color as 6-digit hex (no leading #), e.g. <c>1E3932</c>.</summary>
    public string? BackgroundColor { get; init; }

    /// <summary>Font color as 6-digit hex (no leading #).</summary>
    public string? FontColor { get; init; }

    public bool Bold { get; init; }
    public int? FontSize { get; init; }
}

/// <summary>Controls appearance and column selection for a single-sheet export.</summary>
public class ExcelExportOptions<T>
{
    public string SheetName { get; init; } = "Export";

    /// <summary>Optional bold title row written above the header row.</summary>
    public string? Title { get; init; }

    /// <summary>Optional subtitle row written below the title (e.g., generation timestamp).</summary>
    public string? Subtitle { get; init; }

    public bool FreezeHeaderRow { get; init; } = true;
    public bool AutoFilter { get; init; } = true;
    public bool AlternatingRows { get; init; } = true;

    public ExcelStyle? HeaderStyle { get; init; }
    public ExcelStyle? DataStyle { get; init; }

    public IReadOnlyList<ExcelColumnExport<T>> Columns { get; init; } = [];

    /// <summary>Rows beyond this count are silently truncated.</summary>
    public int MaxRows { get; init; } = 100_000;
}
