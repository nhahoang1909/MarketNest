using MarketNest.Admin.Domain;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Admin.Infrastructure;

/// <summary>
///     Implements <see cref="IReferenceDataReadService" /> with a Redis read-through cache.
///     Queries the <c>AdminReadDbContext</c> and caches results using <c>ICacheService</c>.
///     TTL: 24 hours for all reference data (Tier 1 — changes very rarely).
/// </summary>
internal sealed class ReferenceDataReadService(
    AdminReadDbContext db,
    ICacheService cache) : IReferenceDataReadService
{
    // ── Full list methods ─────────────────────────────────────────────────

    public Task<IReadOnlyList<CountryDto>> GetCountriesAsync(CancellationToken ct = default)
        => cache.GetOrSetAsync(
            CacheKeys.ReferenceData.Countries,
            async () => (IReadOnlyList<CountryDto>)await db.Countries
                .OrderBy(x => x.SortOrder)
                .Select(x => new CountryDto(x.Id, x.Code, x.Label, x.Iso3, x.FlagEmoji, x.SortOrder))
                .ToListAsync(ct),
            CacheKeys.Ttl.ReferenceData,
            ct);

    public Task<IReadOnlyList<GenderDto>> GetGendersAsync(CancellationToken ct = default)
        => cache.GetOrSetAsync(
            CacheKeys.ReferenceData.Genders,
            async () => (IReadOnlyList<GenderDto>)await db.Genders
                .OrderBy(x => x.SortOrder)
                .Select(x => new GenderDto(x.Id, x.Code, x.Label, x.SortOrder))
                .ToListAsync(ct),
            CacheKeys.Ttl.ReferenceData,
            ct);

    public Task<IReadOnlyList<PhoneCountryCodeDto>> GetPhoneCountryCodesAsync(CancellationToken ct = default)
        => cache.GetOrSetAsync(
            CacheKeys.ReferenceData.PhoneCodes,
            async () => (IReadOnlyList<PhoneCountryCodeDto>)await db.PhoneCountryCodes
                .OrderBy(x => x.SortOrder)
                .Select(x => new PhoneCountryCodeDto(
                    x.Id, x.Code, x.Label, x.DialCode, x.CountryCode, x.SortOrder))
                .ToListAsync(ct),
            CacheKeys.Ttl.ReferenceData,
            ct);

    public Task<IReadOnlyList<NationalityDto>> GetNationalitiesAsync(CancellationToken ct = default)
        => cache.GetOrSetAsync(
            CacheKeys.ReferenceData.Nationalities,
            async () => (IReadOnlyList<NationalityDto>)await db.Nationalities
                .OrderBy(x => x.SortOrder)
                .Select(x => new NationalityDto(x.Id, x.Code, x.Label, x.SortOrder))
                .ToListAsync(ct),
            CacheKeys.Ttl.ReferenceData,
            ct);

    public Task<IReadOnlyList<ProductCategoryDto>> GetProductCategoriesAsync(CancellationToken ct = default)
        => cache.GetOrSetAsync(
            CacheKeys.ReferenceData.Categories,
            async () => (IReadOnlyList<ProductCategoryDto>)await db.ProductCategories
                .OrderBy(x => x.SortOrder)
                .Select(x => new ProductCategoryDto(
                    x.Id, x.Code, x.Label, x.Slug, x.SortOrder, x.ParentId, x.IconName))
                .ToListAsync(ct),
            CacheKeys.Ttl.ReferenceData,
            ct);

    public async Task<IReadOnlyList<ProductCategoryDto>> GetRootCategoriesAsync(CancellationToken ct = default)
    {
        var all = await GetProductCategoriesAsync(ct);
        return all.Where(x => x.ParentId == null).ToList();
    }

    public async Task<IReadOnlyList<ProductCategoryDto>> GetChildCategoriesAsync(
        int parentId, CancellationToken ct = default)
    {
        var all = await GetProductCategoriesAsync(ct);
        return all.Where(x => x.ParentId == parentId).ToList();
    }

    // ── Single-item lookup ────────────────────────────────────────────────

    public async Task<CountryDto?> GetCountryAsync(string code, CancellationToken ct = default)
    {
        // Single-item lookup: check in-memory from the full list cache first
        var all = await GetCountriesAsync(ct);
        return all.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ProductCategoryDto?> GetCategoryAsync(int id, CancellationToken ct = default)
    {
        var all = await GetProductCategoriesAsync(ct);
        return all.FirstOrDefault(x => x.Id == id);
    }

    public async Task<ProductCategoryDto?> GetCategoryBySlugAsync(string slug, CancellationToken ct = default)
    {
        var all = await GetProductCategoriesAsync(ct);
        return all.FirstOrDefault(x => string.Equals(x.Slug, slug, StringComparison.OrdinalIgnoreCase));
    }
}

