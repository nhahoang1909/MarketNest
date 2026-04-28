using MarketNest.Base.Domain;

namespace MarketNest.Admin.Domain;

/// <summary>
///     Nationality (separate from Country — semantics differ).
///     Stored in <c>public.nationalities</c>.
/// </summary>
public sealed class Nationality : ReferenceData
{
    private Nationality() { }

    public Nationality(string code, string label, int sortOrder)
        : base(code, label, sortOrder) { }
}

