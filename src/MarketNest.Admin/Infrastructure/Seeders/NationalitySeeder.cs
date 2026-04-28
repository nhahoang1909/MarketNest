using System.Reflection;
using System.Text.Json;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Infrastructure;

/// <summary>Seeds <c>public.nationalities</c> from embedded JSON.</summary>
public class NationalitySeeder(AdminDbContext db) : IDataSeeder
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public int Order => SeederOrder.Nationality;
    public bool RunInProduction => true;
    public string Version => "1.0";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var entries = LoadSeedData();
        var existing = (await db.Nationalities
            .IgnoreQueryFilters()
            .Select(x => x.Code)
            .ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toInsert = entries
            .Where(e => !existing.Contains(e.Code.ToUpperInvariant()))
            .Select((e, i) => new Nationality(e.Code, e.Label, existing.Count + i + 1))
            .ToList();

        if (toInsert.Count == 0) return;

        await db.Nationalities.AddRangeAsync(toInsert, ct);
        await db.SaveChangesAsync(ct);
    }

    private static List<NationalitySeedEntry> LoadSeedData()
    {
        // Nationalities reuse phone_country_codes.json (same label as nationality label)
        using Stream stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(
                "MarketNest.Admin.Infrastructure.Seeders.SeedData.phone_country_codes.json")
            ?? throw new InvalidOperationException("Embedded resource 'phone_country_codes.json' not found.");

        var phoneCodes = JsonSerializer.Deserialize<List<PhoneCodeEntry>>(stream, JsonOptions) ?? [];

        return phoneCodes
            .Select(p => new NationalitySeedEntry(p.CountryCode, p.Label))
            .ToList();
    }

    private sealed record PhoneCodeEntry(string Code, string Label, string DialCode, string CountryCode);
    private sealed record NationalitySeedEntry(string Code, string Label);
}
