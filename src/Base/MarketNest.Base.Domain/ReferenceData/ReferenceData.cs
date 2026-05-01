namespace MarketNest.Base.Domain;

/// <summary>
///     Base class for all Tier 1 reference data entities.
///     Reference data is static lookup values (Country, Gender, etc.) seeded from JSON,
///     owned by the Admin module, consumed by other modules via <c>IReferenceDataReadService</c>.
///     Uses <c>int</c> PK (identity column) — reference data does not need Guid.
///     Implements <see cref="ITrackable"/> so the Admin UI can display who last modified each record.
/// </summary>
public abstract class ReferenceData : Entity<int>, ITrackable
{
    /// <summary>Machine-readable business key: "VN", "MALE". Immutable after creation.</summary>
    public string Code { get; private set; }

    /// <summary>Human-readable display label: "Vietnam", "Male".</summary>
    public string Label { get; private set; }

    /// <summary>Sort position within the list. Lower = higher in dropdown.</summary>
    public int SortOrder { get; private set; }

    /// <summary>Soft-deactivation. Inactive records are filtered by EF query filter.</summary>
    public bool IsActive { get; private set; }

    // ── ITrackable ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; private set; }

    /// <inheritdoc cref="ITrackable.CreatedBy"/>
    /// <remarks>null for records seeded from JSON files — no user actor.</remarks>
    public Guid? CreatedBy { get; private set; }

    /// <inheritdoc cref="ITrackable.ModifiedAt"/>
    /// <remarks>null until the record is first edited after initial seeding.</remarks>
    public DateTimeOffset? ModifiedAt { get; private set; }

    /// <inheritdoc cref="ITrackable.ModifiedBy"/>
    /// <remarks>null until the record is first edited after initial seeding.</remarks>
    public Guid? ModifiedBy { get; private set; }

    void ITrackable.StampCreated(DateTimeOffset at, Guid? by) { CreatedAt = at; CreatedBy = by; }
    void ITrackable.StampModified(DateTimeOffset at, Guid? by) { ModifiedAt = at; ModifiedBy = by; }

    // ── Constructors ──────────────────────────────────────────────────────

    /// <summary>Required by EF Core.</summary>
#pragma warning disable CS8618 // Non-nullable field — EF Core uses this constructor
    protected ReferenceData() { }
#pragma warning restore CS8618

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

