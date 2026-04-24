namespace MarketNest.Core.Common;

/// <summary>
///     Data seeder contract. Seeders run in Order priority at startup.
/// </summary>
public interface IDataSeeder
{
    /// <summary>Lower number = runs first. Convention: 100=Roles, 200=Admin, 300=Categories, 400=Demo.</summary>
    int Order { get; }

    /// <summary>true = safe to run in Production (idempotent, reference data only).</summary>
    bool RunInProduction { get; }

    Task SeedAsync(CancellationToken ct = default);
}
