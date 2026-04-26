using System.Diagnostics;
using Npgsql;
using MarketNest.Base.Infrastructure;
using MarketNest.Base.Common;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Applies pending EF Core migrations and runs <see cref="IDataSeeder" /> implementations on startup.
///     Optimizations over a naive "always migrate + always seed" approach:
///     <list type="bullet">
///         <item>
///             <b>Model hash tracking</b> — computes a SHA-256 hash of each module's EF model snapshot.
///             Migrations are only applied when the hash differs from the stored value in
///             <c>_system.__auto_migration_history</c>.
///         </item>
///         <item>
///             <b>Seed version tracking</b> — each <see cref="IDataSeeder" /> declares a <c>Version</c>.
///             Seeders only execute when their version differs from the stored value in
///             <c>_system.__seed_history</c>.
///         </item>
///         <item>
///             <b>Advisory lock</b> — a PostgreSQL advisory lock prevents concurrent instances (e.g.,
///             multiple pods) from racing during initialization.
///         </item>
///     </list>
/// </summary>
public sealed class DatabaseInitializer(
    IServiceProvider serviceProvider,
    DatabaseTracker tracker,
    IAppLogger<DatabaseInitializer> logger,
    IHostEnvironment env)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var totalSw = Stopwatch.StartNew();
        logger.Info("Database initialization starting");

        // 1. Bootstrap tracking tables (idempotent)
        await tracker.EnsureTrackingTablesExistAsync(ct);

        // 2. Acquire advisory lock to prevent concurrent initialization
        NpgsqlConnection lockConn = await tracker.AcquireAdvisoryLockAsync(ct);

        try
        {
            await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

            await ApplyMigrationsAsync(scope.ServiceProvider, ct);
            await RunSeedersAsync(scope.ServiceProvider, ct);
        }
        finally
        {
            await tracker.ReleaseAdvisoryLockAsync(lockConn);
        }

        totalSw.Stop();
        logger.Info("Database initialization completed in {ElapsedMs}ms", totalSw.ElapsedMilliseconds);
    }

    // ─── Migrations ───────────────────────────────────────────────────────

    private async Task ApplyMigrationsAsync(IServiceProvider scopedProvider, CancellationToken ct)
    {
        var moduleContexts = scopedProvider.GetServices<IModuleDbContext>().ToList();

        if (moduleContexts.Count == 0)
        {
            logger.Info("No module DbContexts registered — skipping migrations");
            return;
        }

        foreach (var module in moduleContexts)
        {
            var contextName = module.ContextName;
            var schemaName = module.SchemaName;
            var dbContext = module.AsDbContext();

            try
            {
                // Ensure the module's schema exists before migration
                // Schema name comes from IModuleDbContext (trusted internal source, not user input)
#pragma warning disable EF1003
                await dbContext.Database.ExecuteSqlRawAsync(
                    "CREATE SCHEMA IF NOT EXISTS \"" + schemaName + "\"", ct);
#pragma warning restore EF1003

                // Compute current model hash
                var currentHash = ModelHasher.ComputeHash(dbContext.Model);
                string? storedHash = await tracker.GetLastModelHashAsync(contextName, ct);

                if (string.Equals(currentHash, storedHash, StringComparison.Ordinal))
                {
                    logger.Info("[{Context}] Model unchanged (hash={Hash}) — skipping migration",
                        contextName, currentHash[..12] + "…");
                    continue;
                }

                // Hash changed (or first run) — check migration state
                var pending = (await dbContext.Database.GetPendingMigrationsAsync(ct)).ToList();
                var applied = (await dbContext.Database.GetAppliedMigrationsAsync(ct)).ToList();

                var sw = Stopwatch.StartNew();

                if (applied.Count == 0 && pending.Count == 0)
                {
                    // No migration files exist — create schema directly from the current model.
                    // This avoids requiring migration files during early development.
                    // Once migrations are added to the project, MigrateAsync() takes over.
                    logger.Info("[{Context}] No migration files found — using EnsureCreated to build schema from model",
                        contextName);
                    await dbContext.Database.EnsureCreatedAsync(ct);
                }
                else if (pending.Count == 0)
                {
                    logger.Info("[{Context}] No pending migrations, but model hash changed — updating tracker",
                        contextName);
                    await tracker.SaveModelHashAsync(contextName, currentHash, ct);
                    continue;
                }
                else
                {
                    logger.Info("[{Context}] Applying {Count} pending migrations: {Migrations}",
                        contextName, pending.Count, string.Join(", ", pending));
                    await dbContext.Database.MigrateAsync(ct);
                }

                sw.Stop();

                // Only save hash AFTER successful migration
                await tracker.SaveModelHashAsync(contextName, currentHash, ct);

                logger.Info("[{Context}] Migrations applied in {ElapsedMs}ms",
                    contextName, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                logger.Critical(ex, "[{Context}] Migration failed — application may be in an inconsistent state",
                    contextName);
                throw;
            }
        }
    }

    // ─── Seeders ──────────────────────────────────────────────────────────

    private async Task RunSeedersAsync(IServiceProvider scopedProvider, CancellationToken ct)
    {
        var seeders = scopedProvider
            .GetServices<IDataSeeder>()
            .OrderBy(s => s.Order)
            .ToList();

        if (seeders.Count == 0)
        {
            logger.Info("No data seeders registered");
            return;
        }

        logger.Info("Evaluating {Count} data seeders", seeders.Count);

        foreach (var seeder in seeders)
        {
            var seederName = seeder.Name;
            var seederVersion = seeder.Version;

            // Skip seeders not suitable for current environment
            if (env.IsProduction() && !seeder.RunInProduction)
            {
                logger.Debug("[{Seeder}] Skipped — not configured for production", seederName);
                continue;
            }

            try
            {
                // Check version against stored history
                string? storedVersion = await tracker.GetLastSeedVersionAsync(seederName, ct);

                if (string.Equals(seederVersion, storedVersion, StringComparison.Ordinal))
                {
                    logger.Debug("[{Seeder}] Version unchanged ({Version}) — skipping",
                        seederName, seederVersion);
                    continue;
                }

                logger.Info("[{Seeder}] Running (version {OldVersion} → {NewVersion}, order={Order})",
                    seederName,
                    storedVersion ?? "(first run)",
                    seederVersion,
                    seeder.Order);

                var sw = Stopwatch.StartNew();
                await seeder.SeedAsync(ct);
                sw.Stop();

                // Only save version AFTER successful seed
                await tracker.SaveSeedVersionAsync(seederName, seederVersion, ct);

                logger.Info("[{Seeder}] Completed in {ElapsedMs}ms", seederName, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "[{Seeder}] Seed failed", seederName);

                // In non-production, fail fast so developers notice
                if (!env.IsProduction())
                    throw;
            }
        }
    }
}
