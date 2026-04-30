namespace MarketNest.Base.Common;

/// <summary>Hint for both import coercion and export cell formatting.</summary>
public enum ExcelColumnFormat
{
    Text,
    Number,
    DecimalNumber,
    Date,
    DateTime,
    Boolean,
    Currency,
    Percentage,
    Url,
    Email
}
