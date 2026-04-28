namespace MarketNest.Base.Domain;

/// <summary>
///     Base class for all Tier 1 reference data entities.
///     Reference data is static lookup values (Country, Gender, etc.) seeded from JSON,
///     owned by the Admin module, consumed by other modules via <c>IReferenceDataReadService</c>.
///     Uses <c>int</c> PK (identity column) — reference data does not need Guid.
/// </summary>
public abstract class ReferenceData : Entity<int>
{
    /// <summary>Machine-readable business key: "VN", "MALE". Immutable after creation.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Human-readable display label: "Vietnam", "Male".</summary>
    public string Label { get; private set; } = string.Empty;

    /// <summary>Sort position within the list. Lower = higher in dropdown.</summary>
    public int SortOrder { get; private set; }

    /// <summary>Soft-deactivation. Inactive records are filtered by EF query filter.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Required by EF Core.</summary>
    protected ReferenceData() { }

    protected ReferenceData(string code, string label, int sortOrder)
    {
        Code = code.ToUpperInvariant().Trim();
        Label = label.Trim();
        SortOrder = sortOrder;
        IsActive = true;
    }

    // ── Domain methods (used in Phase 3 CRUD) ────────────────────────────

    /// <summary>Updates the display label. Code is immutable.</summary>
    public void UpdateLabel(string label) => Label = label.Trim();

    /// <summary>Updates the sort order.</summary>
    public void UpdateSortOrder(int order) => SortOrder = order;

    /// <summary>Reactivates a previously deactivated record.</summary>
    public void Activate() => IsActive = true;

    /// <summary>Soft-deactivates the record. Check FK usage before calling.</summary>
    public void Deactivate() => IsActive = false;
}

