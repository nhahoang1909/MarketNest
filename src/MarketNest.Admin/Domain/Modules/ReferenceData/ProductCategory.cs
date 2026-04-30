using MarketNest.Base.Domain;

namespace MarketNest.Admin.Domain;

/// <summary>
///     Product category with optional parent for a 2-level tree.
///     Stored in <c>public.product_categories</c>.
///     Sellers pick from this Admin-managed list — they cannot create their own categories.
/// </summary>
public sealed class ProductCategory : ReferenceData
{
    /// <summary>URL-safe slug. e.g. "electronics", "clothing". Used in shop filter URLs.</summary>
    public string Slug { get; private set; }

    /// <summary>Optional icon identifier for UI rendering.</summary>
    public string? IconName { get; private set; }  // null = no icon configured

    /// <summary>Parent category id. <c>null</c> means this is a root (top-level) category.</summary>
    public int? ParentId { get; private set; }  // null = root category

#pragma warning disable CS8618 // Non-nullable field — EF Core uses this constructor
    private ProductCategory() { }
#pragma warning restore CS8618

    public ProductCategory(
        string code,
        string label,
        string slug,
        int sortOrder,
        int? parentId = null,
        string? iconName = null)
        : base(code, label, sortOrder)
    {
        Slug = slug.Trim().ToLowerInvariant();
        ParentId = parentId;
        IconName = iconName;
    }

    /// <summary>Moves this category under a different parent (Phase 3 CRUD).</summary>
    public void SetParent(int? parentId) => ParentId = parentId;

    /// <summary>Updates the URL slug (Phase 3 CRUD).</summary>
    public void UpdateSlug(string slug) => Slug = slug.Trim().ToLowerInvariant();

    /// <summary>Updates the icon name (Phase 3 CRUD).</summary>
    public void UpdateIcon(string? iconName) => IconName = iconName;
}

