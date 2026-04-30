namespace MarketNest.Base.Common;

/// <summary>Standard MIME content-type and filename extension constants for Excel/CSV responses.</summary>
public static class ExcelContentTypes
{
    public const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string Xls  = "application/vnd.ms-excel";
    public const string Csv  = "text/csv";

    public const string XlsxExtension = ".xlsx";
    public const string CsvExtension  = ".csv";
}

/// <summary>
///     File-level validation constants shared by the upload UI and the import pipeline.
///     Mirrors <see cref="FieldLimits.FileUpload"/> — keep in sync.
/// </summary>
public static class ExcelUploadRules
{
    /// <summary>Maximum allowed file size for imported Excel/CSV files (10 MB).</summary>
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    /// <summary>Allowed file extensions (lowercase with dot).</summary>
    public static readonly string[] AllowedExtensions = [".xlsx"];

    /// <summary>OOXML ZIP magic bytes — detect real .xlsx regardless of file extension.</summary>
    public static readonly byte[] XlsxMagicBytes = [0x50, 0x4B, 0x03, 0x04];

    /// <summary>Minimum required bytes to read magic bytes from a stream.</summary>
    public const int MagicByteCount = 4;
}

