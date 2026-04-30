using System.Reflection;
using System.Text.Json;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Infrastructure;

/// <summary>
///     Seeds <c>public.product_categories</c> from embedded JSON.
///     Inserts root categories first, then child categories (two-pass to resolve parentId).
/// </summary>
public class ProductCategorySeeder(AdminDbContext db) : IDataSeeder
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    // Must run after other reference data to keep ordering clean
    public int Order => SeederOrder.ProductCategory;
    public bool RunInProduction => true;
    public string Version => "1.0";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var entries = LoadSeedData();
        var existing = (await db.ProductCategories
            .IgnoreQueryFilters()
            .Select(x => x.Code)
            .ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toInsert = entries
            .Where(e => !existing.Contains(e.Code.ToUpperInvariant()))
            .ToList();

        if (toInsert.Count == 0) return;

        // Pass 1: insert root categories (parentId == null in JSON)
        var roots = toInsert
            .Where(e => e.ParentId == null)
            .Select((e, i) => new ProductCategory(e.Code, e.Label, e.Slug, i + 1,
                parentId: null, iconName: e.IconName))
            .ToList();

        await db.ProductCategories.AddRangeAsync(roots, ct);
        await db.SaveChangesAsync(ct);

        // Pass 2: insert children, resolving parentId by parent Code
        var savedRoots = await db.ProductCategories
            .IgnoreQueryFilters()
            .Where(x => roots.Select(r => r.Code).Contains(x.Code))
            .ToDictionaryAsync(x => x.Code, ct);

        int childSort = roots.Count + 1;
        var children = toInsert
            .Where(e => e.ParentId != null)
            .Select(e =>
            {
                int? parentId = savedRoots.TryGetValue(e.ParentId!.ToUpperInvariant(), out var parent)
                    ? parent.Id
                    : null;

                return new ProductCategory(e.Code, e.Label, e.Slug, childSort++,
                    parentId: parentId, iconName: e.IconName);
            })
            .ToList();

        if (children.Count == 0) return;

        await db.ProductCategories.AddRangeAsync(children, ct);
        await db.SaveChangesAsync(ct);
    }

    private static List<CategorySeedEntry> LoadSeedData()
    {
        using Stream stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(
                "MarketNest.Admin.Infrastructure.Seeders.SeedData.product_categories.json")
            ?? throw new InvalidOperationException(
                "Embedded resource 'product_categories.json' not found.");

        return JsonSerializer.Deserialize<List<CategorySeedEntry>>(stream, JsonOptions) ?? [];
    }

    private sealed record CategorySeedEntry(
        string Code,
        string Label,
        string Slug,
        string? ParentId,
        string? IconName);
}
