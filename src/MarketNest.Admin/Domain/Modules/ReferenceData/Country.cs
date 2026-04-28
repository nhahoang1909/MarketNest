using MarketNest.Base.Domain;

namespace MarketNest.Admin.Domain;

/// <summary>ISO 3166-1 country. Stored in <c>public.countries</c>.</summary>
public sealed class Country : ReferenceData
{
    /// <summary>ISO 3166-1 alpha-3 code. e.g. "VNM", "USA".</summary>
    public string Iso3 { get; private set; } = string.Empty;

    /// <summary>Unicode flag emoji. e.g. "🇻🇳".</summary>
    public string FlagEmoji { get; private set; } = string.Empty;

    /// <summary>Required by EF Core.</summary>
    private Country() { }

    public Country(string code, string label, string iso3, string flagEmoji, int sortOrder)
        : base(code, label, sortOrder)
    {
        Iso3 = iso3.ToUpperInvariant().Trim();
        FlagEmoji = flagEmoji;
    }
}

