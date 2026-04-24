using System.Diagnostics;
using MarketNest.Core.Common;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Web.Infrastructure;

/// <summary>
/// Applies pending EF Core migrations and runs all <see cref="IDataSeeder"/> implementations on startup.
/// Inspired by ESMS DatabaseServer but simplified for single-tenant modular monolith.
///
/// Execution order:
///   1. EnsureCreated — verify DB connectivity
///   2. MigrateAsync — apply all pending EF Core migrations
///   3. SeedAsync — run seeders ordered by <see cref="IDataSeeder.Order"/>
/// </summary>
public sealed class DatabaseInitializer(
    IServiceProvider serviceProvider,
    ILogger<DatabaseInitializer> logger,
    IHostEnvironment env)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        logger.LogInformation("Database initialization starting");

        await using var scope = serviceProvider.CreateAsyncScope();

        // ── Step 1: Apply pending migrations ──────────────────────────
        await ApplyMigrationsAsync(scope.ServiceProvider, ct);

        // ── Step 2: Run data seeders ──────────────────────────────────
        await RunSeedersAsync(scope.ServiceProvider, ct);

        sw.Stop();
        logger.LogInformation("Database initialization completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);
    }

    private async Task ApplyMigrationsAsync(IServiceProvider scopedProvider, CancellationToken ct)
    {
        var dbContext = scopedProvider.GetRequiredService<DbContext>();

        try
        {
            var pending = (await dbContext.Database.GetPendingMigrationsAsync(ct)).ToList();

            if (pending.Count == 0)
            {
                logger.LogInformation("No pending migrations");
                return;
            }

            logger.LogInformation("Applying {Count} pending migrations: {Migrations}",
                pending.Count, string.Join(", ", pending));

            await dbContext.Database.MigrateAsync(ct);

            logger.LogInformation("Migrations applied successfully");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Migration failed — application may be in an inconsistent state");
            throw;
        }
    }

    private async Task RunSeedersAsync(IServiceProvider scopedProvider, CancellationToken ct)
    {
        var seeders = scopedProvider
            .GetServices<IDataSeeder>()
            .OrderBy(s => s.Order)
            .ToList();

        if (seeders.Count == 0)
        {
            logger.LogInformation("No data seeders registered");
            return;
        }

        logger.LogInformation("Running {Count} data seeders", seeders.Count);

        foreach (var seeder in seeders)
        {
            var seederName = seeder.GetType().Name;

            if (env.IsProduction() && !seeder.RunInProduction)
            {
                logger.LogDebug("Skipping seeder {Seeder} (not configured for production)", seederName);
                continue;
            }

            try
            {
                logger.LogInformation("Running seeder: {Seeder} (Order={Order}, RunInProd={RunInProd})",
                    seederName, seeder.Order, seeder.RunInProduction);

                var seederSw = Stopwatch.StartNew();
                await seeder.SeedAsync(ct);
                seederSw.Stop();

                logger.LogInformation("Seeder {Seeder} completed in {ElapsedMs}ms",
                    seederName, seederSw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Seeder {Seeder} failed", seederName);

                // In production, a failed seeder should not crash the app.
                // In development, fail fast so devs notice immediately.
                if (!env.IsProduction())
                    throw;
            }
        }
    }
}
