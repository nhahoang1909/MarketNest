using System.Diagnostics;
using Npgsql;
using MarketNest.Base.Infrastructure;
using MarketNest.Base.Common;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Applies pending EF Core migrations and runs <see cref="IDataSeeder" /> implementations on startup.
/// </summary>
public sealed partial class DatabaseInitializer(
    IServiceProvider serviceProvider,
    DatabaseTracker tracker,
    IAppLogger<DatabaseInitializer> logger,
    IHostEnvironment env)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var totalSw = Stopwatch.StartNew();
        Log.InfoStart(logger);

        await tracker.EnsureTrackingTablesExistAsync(ct);
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
        Log.InfoCompleted(logger, totalSw.ElapsedMilliseconds);
    }

    private async Task ApplyMigrationsAsync(IServiceProvider scopedProvider, CancellationToken ct)
    {
        var moduleContexts = scopedProvider.GetServices<IModuleDbContext>().ToList();

        if (moduleContexts.Count == 0)
        {
            Log.InfoNoContexts(logger);
            return;
        }

        foreach (var module in moduleContexts)
        {
            var contextName = module.ContextName;
            var schemaName = module.SchemaName;
            var dbContext = module.AsDbContext();

            try
            {
#pragma warning disable EF1003
                await dbContext.Database.ExecuteSqlRawAsync(
                    "CREATE SCHEMA IF NOT EXISTS \"" + schemaName + "\"", ct);
#pragma warning restore EF1003

                var currentHash = ModelHasher.ComputeHash(dbContext.Model);
                string? storedHash = await tracker.GetLastModelHashAsync(contextName, ct);

                if (string.Equals(currentHash, storedHash, StringComparison.Ordinal))
                {
                    Log.InfoModelUnchanged(logger, contextName, currentHash[..12] + "…");
                    continue;
                }

                var pending = (await dbContext.Database.GetPendingMigrationsAsync(ct)).ToList();
                var applied = (await dbContext.Database.GetAppliedMigrationsAsync(ct)).ToList();
                var sw = Stopwatch.StartNew();

                if (applied.Count == 0 && pending.Count == 0)
                {
                    Log.InfoNoMigrationFiles(logger, contextName);
                    await dbContext.Database.EnsureCreatedAsync(ct);
                }
                else if (pending.Count == 0)
                {
                    Log.InfoHashChangedNoMigrations(logger, contextName);
                    await tracker.SaveModelHashAsync(contextName, currentHash, ct);
                    continue;
                }
                else
                {
                    Log.InfoApplyingMigrations(logger, contextName, pending.Count,
                        string.Join(", ", pending));
                    await dbContext.Database.MigrateAsync(ct);
                }

                sw.Stop();
                await tracker.SaveModelHashAsync(contextName, currentHash, ct);
                Log.InfoMigrationsApplied(logger, contextName, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                Log.CriticalMigrationFailed(logger, contextName, ex);
                throw;
            }
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
            Log.InfoNoSeeders(logger);
            return;
        }

        Log.InfoSeedEvaluating(logger, seeders.Count);

        foreach (var seeder in seeders)
        {
            var seederName = seeder.Name;
            var seederVersion = seeder.Version;

            if (env.IsProduction() && !seeder.RunInProduction)
            {
                Log.DebugSeedSkippedProd(logger, seederName);
                continue;
            }

            try
            {
                string? storedVersion = await tracker.GetLastSeedVersionAsync(seederName, ct);

                if (string.Equals(seederVersion, storedVersion, StringComparison.Ordinal))
                {
                    Log.DebugSeedSkippedVersion(logger, seederName, seederVersion);
                    continue;
                }

                Log.InfoSeedRunning(logger, seederName,
                    storedVersion ?? "(first run)", seederVersion, seeder.Order);

                var sw = Stopwatch.StartNew();
                await seeder.SeedAsync(ct);
                sw.Stop();

                await tracker.SaveSeedVersionAsync(seederName, seederVersion, ct);
                Log.InfoSeedCompleted(logger, seederName, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                Log.ErrorSeedFailed(logger, seederName, ex);
                if (!env.IsProduction())
                    throw;
            }
        }
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.DbInitStart, LogLevel.Information,
            "Database initialization starting")]
        public static partial void InfoStart(ILogger logger);

        [LoggerMessage((int)LogEventId.DbInitCompleted, LogLevel.Information,
            "Database initialization completed in {ElapsedMs}ms")]
        public static partial void InfoCompleted(ILogger logger, long elapsedMs);

        [LoggerMessage((int)LogEventId.DbInitNoContexts, LogLevel.Information,
            "No module DbContexts registered — skipping migrations")]
        public static partial void InfoNoContexts(ILogger logger);

        [LoggerMessage((int)LogEventId.DbInitModelUnchanged, LogLevel.Information,
            "[{Context}] Model unchanged (hash={Hash}) — skipping migration")]
        public static partial void InfoModelUnchanged(ILogger logger, string context, string hash);

        [LoggerMessage((int)LogEventId.DbInitNoMigrationFiles, LogLevel.Information,
            "[{Context}] No migration files found — using EnsureCreated to build schema from model")]
        public static partial void InfoNoMigrationFiles(ILogger logger, string context);

        [LoggerMessage((int)LogEventId.DbInitHashChangedNoMigrations, LogLevel.Information,
            "[{Context}] No pending migrations, but model hash changed — updating tracker")]
        public static partial void InfoHashChangedNoMigrations(ILogger logger, string context);

        [LoggerMessage((int)LogEventId.DbInitApplyingMigrations, LogLevel.Information,
            "[{Context}] Applying {Count} pending migrations: {Migrations}")]
        public static partial void InfoApplyingMigrations(ILogger logger, string context, int count, string migrations);

        [LoggerMessage((int)LogEventId.DbInitMigrationsApplied, LogLevel.Information,
            "[{Context}] Migrations applied in {ElapsedMs}ms")]
        public static partial void InfoMigrationsApplied(ILogger logger, string context, long elapsedMs);

        [LoggerMessage((int)LogEventId.DbInitMigrationCritical, LogLevel.Critical,
            "[{Context}] Migration failed — application may be in an inconsistent state")]
        public static partial void CriticalMigrationFailed(ILogger logger, string context, Exception ex);

        [LoggerMessage((int)LogEventId.DbInitNoSeeders, LogLevel.Information,
            "No data seeders registered")]
        public static partial void InfoNoSeeders(ILogger logger);

        [LoggerMessage((int)LogEventId.DbInitSeedEvaluating, LogLevel.Information,
            "Evaluating {Count} data seeders")]
        public static partial void InfoSeedEvaluating(ILogger logger, int count);

        [LoggerMessage((int)LogEventId.DbInitSeedSkippedProd, LogLevel.Debug,
            "[{Seeder}] Skipped — not configured for production")]
        public static partial void DebugSeedSkippedProd(ILogger logger, string seeder);

        [LoggerMessage((int)LogEventId.DbInitSeedSkippedVersion, LogLevel.Debug,
            "[{Seeder}] Version unchanged ({Version}) — skipping")]
        public static partial void DebugSeedSkippedVersion(ILogger logger, string seeder, string version);

        [LoggerMessage((int)LogEventId.DbInitSeedRunning, LogLevel.Information,
            "[{Seeder}] Running (version {OldVersion} → {NewVersion}, order={Order})")]
        public static partial void InfoSeedRunning(ILogger logger, string seeder,
            string oldVersion, string newVersion, int order);

        [LoggerMessage((int)LogEventId.DbInitSeedCompleted, LogLevel.Information,
            "[{Seeder}] Completed in {ElapsedMs}ms")]
        public static partial void InfoSeedCompleted(ILogger logger, string seeder, long elapsedMs);

        [LoggerMessage((int)LogEventId.DbInitSeedFailed, LogLevel.Error,
            "[{Seeder}] Seed failed")]
        public static partial void ErrorSeedFailed(ILogger logger, string seeder, Exception ex);
    }
}
