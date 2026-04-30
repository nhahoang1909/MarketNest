using System.Reflection;
using System.Text.Json;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Infrastructure;

/// <summary>Seeds <c>public.phone_country_codes</c> from embedded JSON.</summary>
public class PhoneCountryCodeSeeder(AdminDbContext db) : IDataSeeder
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public int Order => SeederOrder.PhoneCountryCode;
    public bool RunInProduction => true;
    public string Version => "1.0";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var entries = LoadSeedData();
        var existing = (await db.PhoneCountryCodes
            .IgnoreQueryFilters()
            .Select(x => x.Code)
            .ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toInsert = entries
            .Where(e => !existing.Contains(e.Code.ToUpperInvariant()))
            .Select((e, i) => new PhoneCountryCode(
                e.Code, e.Label, e.DialCode, e.CountryCode, existing.Count + i + 1))
            .ToList();

        if (toInsert.Count == 0) return;

        await db.PhoneCountryCodes.AddRangeAsync(toInsert, ct);
        await db.SaveChangesAsync(ct);
    }

    private static List<PhoneCodeSeedEntry> LoadSeedData()
    {
        using Stream stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(
                "MarketNest.Admin.Infrastructure.Seeders.SeedData.phone_country_codes.json")
            ?? throw new InvalidOperationException(
                "Embedded resource 'phone_country_codes.json' not found.");

        return JsonSerializer.Deserialize<List<PhoneCodeSeedEntry>>(stream, JsonOptions) ?? [];
    }

    private sealed record PhoneCodeSeedEntry(
        string Code, string Label, string DialCode, string CountryCode);
}
