using MarketNest.Base.Domain;

namespace MarketNest.Admin.Domain;

/// <summary>Phone country dial code. Stored in <c>public.phone_country_codes</c>.</summary>
public sealed class PhoneCountryCode : ReferenceData
{
    /// <summary>International dial prefix. e.g. "+84", "+1".</summary>
    public string DialCode { get; private set; }

    /// <summary>
    ///     Display link to the corresponding Country code (not a FK — reference only).
    ///     e.g. "VN", "US".
    /// </summary>
    public string CountryCode { get; private set; }

#pragma warning disable CS8618 // Non-nullable field — EF Core uses this constructor
    private PhoneCountryCode() { }
#pragma warning restore CS8618

    public PhoneCountryCode(string code, string label, string dialCode, string countryCode, int sortOrder)
        : base(code, label, sortOrder)
    {
        DialCode = dialCode;
        CountryCode = countryCode.ToUpperInvariant().Trim();
    }
}

