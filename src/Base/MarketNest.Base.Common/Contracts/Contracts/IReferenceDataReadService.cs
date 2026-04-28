namespace MarketNest.Base.Common;

// ── Reference Data DTOs ──────────────────────────────────────────────────────
// Strongly-typed per reference data type. Defined here so all modules can use them
// without depending on the Admin module.

public record CountryDto(int Id, string Code, string Label, string Iso3, string FlagEmoji, int SortOrder);

public record GenderDto(int Id, string Code, string Label, int SortOrder);

public record PhoneCountryCodeDto(int Id, string Code, string Label, string DialCode, string CountryCode,
    int SortOrder);

public record NationalityDto(int Id, string Code, string Label, int SortOrder);

public record ProductCategoryDto(
    int Id,
    string Code,
    string Label,
    string Slug,
    int SortOrder,
    int? ParentId,
    string? IconName);

// ── Contract ─────────────────────────────────────────────────────────────────

/// <summary>
///     Cross-module read contract for Tier 1 reference data.
///     Implemented by <c>ReferenceDataReadService</c> in the Admin module;
///     consumed by any module that needs dropdown data or validation lookups.
///     All methods are Redis-cached with a 24-hour TTL.
/// </summary>
public interface IReferenceDataReadService
{
    // ── Full list methods — all cached 24h ────────────────────────────────
    Task<IReadOnlyList<CountryDto>> GetCountriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<GenderDto>> GetGendersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PhoneCountryCodeDto>> GetPhoneCountryCodesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NationalityDto>> GetNationalitiesAsync(CancellationToken ct = default);

    /// <summary>Returns all active product categories (flat list, ordered by SortOrder).</summary>
    Task<IReadOnlyList<ProductCategoryDto>> GetProductCategoriesAsync(CancellationToken ct = default);

    /// <summary>Returns only root (top-level) categories — ParentId is null.</summary>
    Task<IReadOnlyList<ProductCategoryDto>> GetRootCategoriesAsync(CancellationToken ct = default);

    /// <summary>Returns direct children of the specified parent.</summary>
    Task<IReadOnlyList<ProductCategoryDto>> GetChildCategoriesAsync(int parentId, CancellationToken ct = default);

    // ── Single lookup — used for validation ───────────────────────────────
    Task<CountryDto?> GetCountryAsync(string code, CancellationToken ct = default);
    Task<ProductCategoryDto?> GetCategoryAsync(int id, CancellationToken ct = default);
    Task<ProductCategoryDto?> GetCategoryBySlugAsync(string slug, CancellationToken ct = default);
}

