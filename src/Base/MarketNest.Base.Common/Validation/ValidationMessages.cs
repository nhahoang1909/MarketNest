namespace MarketNest.Base.Common;

/// <summary>
///     Centralized, human-readable validation error messages.
///     All FluentValidation rules must use these methods — never inline string literals.
/// </summary>
public static class ValidationMessages
{
    // ── Required ──────────────────────────────────────────────────────────
    public static string Required(string fieldName)
        => $"{fieldName} is required.";

    // ── Length ────────────────────────────────────────────────────────────
    public static string MaxLength(string fieldName, int max)
        => $"{fieldName} must not exceed {max} characters.";

    public static string MinLength(string fieldName, int min)
        => $"{fieldName} must be at least {min} characters.";

    public static string ExactLength(string fieldName, int length)
        => $"{fieldName} must be exactly {length} characters.";

    public static string LengthBetween(string fieldName, int min, int max)
        => $"{fieldName} must be between {min} and {max} characters.";

    // ── Numeric ───────────────────────────────────────────────────────────
    public static string MustBePositive(string fieldName)
        => $"{fieldName} must be greater than zero.";

    public static string MinValue(string fieldName, object min)
        => $"{fieldName} must be at least {min}.";

    public static string MaxValue(string fieldName, object max)
        => $"{fieldName} must not exceed {max}.";

    public static string RangeBetween(string fieldName, object min, object max)
        => $"{fieldName} must be between {min} and {max}.";

    public static string MaxDecimalPlaces(string fieldName, int places)
        => $"{fieldName} must not have more than {places} decimal places.";

    // ── Format ────────────────────────────────────────────────────────────
    public static string InvalidFormat(string fieldName, string expectedFormat)
        => $"{fieldName} is not in a valid format. Expected: {expectedFormat}.";

    public static string InvalidSlugFormat(string fieldName)
        => $"{fieldName} may only contain lowercase letters, numbers, and hyphens.";

    public static string InvalidPhoneFormat()
        => "Phone number must be in E.164 format (e.g., +84912345678).";

    public static string InvalidEmailFormat()
        => "Email address is not valid.";

    // ── Date ──────────────────────────────────────────────────────────────
    public static string DateMustBeBefore(string fieldName, string otherField)
        => $"{fieldName} must be before {otherField}.";

    public static string DateMustBeAfter(string fieldName, string otherField)
        => $"{fieldName} must be after {otherField}.";

    public static string DateMustBeInFuture(string fieldName)
        => $"{fieldName} must be a future date.";

    public static string DateMustNotBePast(string fieldName)
        => $"{fieldName} cannot be in the past.";

    // ── File ──────────────────────────────────────────────────────────────
    public static string InvalidFileType(string fieldName, string allowed)
        => $"{fieldName} must be one of the following formats: {allowed}.";

    public static string FileTooLarge(string fieldName, string maxSize)
        => $"{fieldName} must not exceed {maxSize}.";

    // ── Collection ────────────────────────────────────────────────────────
    public static string CollectionMinItems(string fieldName, int min)
        => $"At least {min} {fieldName.ToLowerInvariant()} is required.";

    public static string CollectionMaxItems(string fieldName, int max)
        => $"Maximum {max} {fieldName.ToLowerInvariant()} allowed.";

    // ── Identity / Reference ──────────────────────────────────────────────
    public static string InvalidId(string fieldName)
        => $"{fieldName} is not a valid identifier.";

    public static string NotFound(string entityName)
        => $"{entityName} not found.";

    public static string AlreadyExists(string entityName)
        => $"{entityName} already exists.";

    // ── Excel Import ─────────────────────────────────────────────────────
    public static string ExcelColumnRequired(string columnName, int rowNumber)
        => $"Row {rowNumber}: '{columnName}' is required.";

    public static string ExcelColumnMaxLength(string columnName, int max, int rowNumber)
        => $"Row {rowNumber}: '{columnName}' must not exceed {max} characters.";

    public static string ExcelColumnInvalidFormat(string columnName, string expectedFormat, int rowNumber)
        => $"Row {rowNumber}: '{columnName}' has invalid format. Expected: {expectedFormat}.";

    public static string ExcelColumnRangeBetween(string columnName, object min, object max, int rowNumber)
        => $"Row {rowNumber}: '{columnName}' must be between {min} and {max}.";

    public static string ExcelColumnNotFound(string columnName)
        => $"Required column '{columnName}' not found in the spreadsheet.";

    public static string ExcelSheetNotFound(string sheetName)
        => $"Sheet '{sheetName}' not found in the uploaded file.";

    public static string ExcelFileEmpty()
        => "The uploaded file contains no data rows.";

    public static string ExcelTooManyRows(int max)
        => $"Import file must not exceed {max} rows per batch.";
}

