namespace MarketNest.Base.Common;

/// <summary>
///     Structured <see cref="Error"/> factory for all Excel import/export failures.
///     Use these error codes as the canonical domain errors for the import pipeline.
/// </summary>
public static class ExcelErrors
{
    public static Error FileTooLarge(long sizeBytes) =>
        new("EXCEL.FILE_TOO_LARGE",
            $"File size {sizeBytes / 1024 / 1024} MB exceeds the {ExcelUploadRules.MaxFileSizeBytes / 1024 / 1024} MB limit.");

    public static Error InvalidFileType =>
        new("EXCEL.INVALID_FILE_TYPE", "Only .xlsx files are supported.");

    public static Error InvalidFileMagicBytes =>
        new("EXCEL.INVALID_MAGIC_BYTES", "File does not appear to be a valid .xlsx file.");

    public static Error MissingRequiredColumns(IEnumerable<string> cols) =>
        new("EXCEL.MISSING_COLUMNS",
            $"Required columns are missing: {string.Join(", ", cols)}");

    public static Error NoValidRows =>
        new("EXCEL.NO_VALID_ROWS", "No valid rows were found in the file.");

    public static Error ExceedsMaxRows(int max) =>
        new("EXCEL.EXCEEDS_MAX_ROWS",
            $"File contains more than {max:N0} rows. Split it into smaller files.");

    public static Error VirusScanFailed =>
        new("EXCEL.VIRUS_DETECTED",
            "The uploaded file failed the antivirus scan and was rejected.", ErrorType.Unexpected);

    public static Error ParseFailed(string detail) =>
        new("EXCEL.PARSE_FAILED", $"Could not read the file: {detail}", ErrorType.Unexpected);
}

