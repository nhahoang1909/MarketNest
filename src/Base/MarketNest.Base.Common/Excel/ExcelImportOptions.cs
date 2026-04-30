namespace MarketNest.Base.Common;

/// <summary>
///     Options for the template-less import path where column-to-property mapping is derived
///     automatically from property names or <see cref="ExcelColumnAttribute"/> decorations.
/// </summary>
public class ExcelImportOptions
{
    public int HeaderRowIndex { get; init; } = 1;
    public int DataStartRowIndex { get; init; } = 2;

    /// <summary>Worksheet to read; <c>null</c> = first sheet.</summary>
    public string? SheetName { get; init; }

    public bool IgnoreEmptyRows { get; init; } = true;
    public bool TrimValues { get; init; } = true;
    public bool CaseInsensitiveHeaders { get; init; } = true;
    public int MaxRows { get; init; } = 10_000;

    /// <summary>
    ///     When true, property-to-column binding uses the <see cref="ExcelColumnAttribute.Header"/>
    ///     value if present, falling back to the property name.
    /// </summary>
    public bool UseAttributeMapping { get; init; } = true;
}

/// <summary>
///     Decoration for DTO properties used in template-less imports
///     (<see cref="ExcelImportOptions.UseAttributeMapping"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ExcelColumnAttribute(string header) : Attribute
{
    public string Header { get; } = header;
    public bool Required { get; init; }
    public ExcelColumnFormat Format { get; init; }
}
