using System.Reflection;
using System.Text.Json;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Infrastructure;

/// <summary>Seeds <c>public.genders</c> from embedded JSON.</summary>
public class GenderSeeder(AdminDbContext db) : IDataSeeder
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public int Order => SeederOrder.Gender;
    public bool RunInProduction => true;
    public string Version => "1.0";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var entries = LoadSeedData();
        var existing = (await db.Genders
            .IgnoreQueryFilters()
            .Select(x => x.Code)
            .ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toInsert = entries
            .Where(e => !existing.Contains(e.Code.ToUpperInvariant()))
            .Select((e, i) => new Gender(e.Code, e.Label, existing.Count + i + 1))
            .ToList();

        if (toInsert.Count == 0) return;

        await db.Genders.AddRangeAsync(toInsert, ct);
        await db.SaveChangesAsync(ct);
    }

    private static List<GenderSeedEntry> LoadSeedData()
    {
        using Stream stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(
                "MarketNest.Admin.Infrastructure.Seeders.SeedData.genders.json")
            ?? throw new InvalidOperationException("Embedded resource 'genders.json' not found.");

        return JsonSerializer.Deserialize<List<GenderSeedEntry>>(stream, JsonOptions) ?? [];
    }

    private sealed record GenderSeedEntry(string Code, string Label);
}
