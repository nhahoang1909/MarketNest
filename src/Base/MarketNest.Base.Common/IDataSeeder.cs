namespace MarketNest.Base.Common;

/// <summary>
///     Data seeder contract. Seeders run in Order priority at startup.
///     The <see cref="Version" /> property enables change-tracking: seeders only
///     re-run when their version changes (tracked in <c>_system.__seed_history</c>).
/// </summary>
public interface IDataSeeder
{
    /// <summary>Lower number = runs first. Convention: 100=Roles, 200=Admin, 300=Categories, 400=Demo.</summary>
    int Order { get; }

    /// <summary>true = safe to run in Production (idempotent, reference data only).</summary>
    bool RunInProduction { get; }

    /// <summary>
    ///     Bump this when seed data changes (e.g., "1.0", "2025.04.25").
    ///     The initializer compares this against the stored version in <c>__seed_history</c>.
    ///     If changed (or first run), the seeder executes and the new version is recorded.
    /// </summary>
    string Version { get; }

    /// <summary>
    ///     Unique name for this seeder used as the key in <c>__seed_history</c>.
    ///     Defaults to the fully qualified type name.
    /// </summary>
    string Name => GetType().FullName ?? GetType().Name;

    Task SeedAsync(CancellationToken ct = default);
}
