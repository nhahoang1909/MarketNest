namespace MarketNest.Base.Common;

/// <summary>Base class for multi-sheet export sheet definitions.</summary>
public abstract class ExcelSheetDefinition
{
    public string SheetName { get; init; } = "Sheet";
}

/// <summary>Typed sheet definition binding data rows to a strongly-typed export options object.</summary>
public class ExcelSheetDefinition<T> : ExcelSheetDefinition
{
    public IEnumerable<T> Data { get; init; } = [];
    public ExcelExportOptions<T> Options { get; init; } = new();
}

/// <summary>Workbook-level settings for multi-sheet exports.</summary>
public class ExcelWorkbookOptions
{
    public string? Author { get; init; }
    public string? Title { get; init; }

    /// <summary>Protect the workbook structure (sheet add/delete) with a password.</summary>
    public bool ProtectWorkbook { get; init; }

    /// <summary>Password for workbook or sheet protection. Keep null if protection is not needed.</summary>
    public string? Password { get; init; }
}
