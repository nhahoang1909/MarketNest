using System.Reflection;
using System.Text.Json;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Infrastructure;

/// <summary>
///     Seeds the <c>public.countries</c> table from embedded JSON.
///     Idempotent: skips codes that already exist.
/// </summary>
public class CountrySeeder(AdminDbContext db) : IDataSeeder
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public int Order => SeederOrder.Country;
    public bool RunInProduction => true;
    public string Version => "1.0";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var entries = LoadSeedData();
        var existing = (await db.Countries
            .IgnoreQueryFilters()
            .Select(x => x.Code)
            .ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toInsert = entries
            .Where(e => !existing.Contains(e.Code.ToUpperInvariant()))
            .Select((e, i) => new Country(e.Code, e.Label, e.Iso3, e.FlagEmoji, existing.Count + i + 1))
            .ToList();

        if (toInsert.Count == 0) return;

        await db.Countries.AddRangeAsync(toInsert, ct);
        await db.SaveChangesAsync(ct);
    }

    private static List<CountrySeedEntry> LoadSeedData()
    {
        using Stream stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream($"MarketNest.Admin.Infrastructure.Seeders.SeedData.countries.json")
            ?? throw new InvalidOperationException("Embedded resource 'countries.json' not found.");

        return JsonSerializer.Deserialize<List<CountrySeedEntry>>(stream, JsonOptions) ?? [];
    }

    private sealed record CountrySeedEntry(string Code, string Label, string Iso3, string FlagEmoji);
}
