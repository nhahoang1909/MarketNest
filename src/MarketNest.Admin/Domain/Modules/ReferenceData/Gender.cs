using MarketNest.Base.Domain;

namespace MarketNest.Admin.Domain;

/// <summary>Gender option. Stored in <c>public.genders</c>.</summary>
public sealed class Gender : ReferenceData
{
    private Gender() { }

    public Gender(string code, string label, int sortOrder)
        : base(code, label, sortOrder) { }
}

