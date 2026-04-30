using MarketNest.Base.Domain;

namespace MarketNest.Admin.Domain;

/// <summary>ISO 3166-1 country. Stored in <c>public.countries</c>.</summary>
public sealed class Country : ReferenceData
{
    /// <summary>ISO 3166-1 alpha-3 code. e.g. "VNM", "USA".</summary>
    public string Iso3 { get; private set; }

    /// <summary>Unicode flag emoji. e.g. "🇻🇳".</summary>
    public string FlagEmoji { get; private set; }

    /// <summary>Required by EF Core.</summary>
#pragma warning disable CS8618 // Non-nullable field — EF Core uses this constructor
    private Country() { }
#pragma warning restore CS8618

    public Country(string code, string label, string iso3, string flagEmoji, int sortOrder)
        : base(code, label, sortOrder)
    {
        Iso3 = iso3.ToUpperInvariant().Trim();
        FlagEmoji = flagEmoji;
    }
}

