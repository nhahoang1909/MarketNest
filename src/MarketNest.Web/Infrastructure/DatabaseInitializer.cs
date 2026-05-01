using System.Diagnostics;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

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
                var createSchemaSql = BuildCreateSchemaSql(schemaName);
                await dbContext.Database.ExecuteSqlRawAsync(createSchemaSql, ct);
#pragma warning restore EF1003

                var currentHash = ModelHasher.ComputeHash(dbContext.Model);
                string? storedHash = await tracker.GetLastModelHashAsync(contextName, ct);

                if (string.Equals(currentHash, storedHash, StringComparison.Ordinal))
                {
                    // Verify tables actually exist — hash may be stale from a broken previous run
                    if (!env.IsProduction() && !await ContextTablesExistAsync(dbContext, ct))
                    {
                        Log.WarnStaleHash(logger, contextName);
                        await DropContextTablesAsync(dbContext, ct);
                        var creator = dbContext.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>();
                        await creator.CreateTablesAsync(ct);
                        await tracker.ClearAllSeedVersionsAsync(ct);
                        await tracker.SaveModelHashAsync(contextName, currentHash, ct);
                        Log.InfoMigrationsApplied(logger, contextName, 0);
                    }
                    else
                    {
                        Log.InfoModelUnchanged(logger, contextName, currentHash[..12] + "…");
                    }
                    continue;
                }

                var pending = (await dbContext.Database.GetPendingMigrationsAsync(ct)).ToList();
                var applied = (await dbContext.Database.GetAppliedMigrationsAsync(ct)).ToList();
                var sw = Stopwatch.StartNew();

                if (applied.Count == 0 && pending.Count == 0)
                {
                    Log.InfoNoMigrationFiles(logger, contextName);

                    var created = await dbContext.Database.EnsureCreatedAsync(ct);

                    // EnsureCreated returns true if the database was created, false if it already existed.
                    // When the DB exists but EnsureCreated returns false, we must check if this context's
                    // tables actually exist. If not, create them. If yes but hash changed (or this is first run),
                    // drop and recreate in dev.
                    if (!created)
                    {
                        var tablesExist = await ContextTablesExistAsync(dbContext, ct);

                        if (!tablesExist)
                        {
                            // Tables don't exist — create them from the model (first run with empty DB)
                            Log.InfoCreatingMissingTables(logger, contextName);
                            var creator = dbContext.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>();
                            await creator.CreateTablesAsync(ct);
                            // Clear seed versions so seeders re-populate the new tables
                            await tracker.ClearAllSeedVersionsAsync(ct);
                        }
                        else if (storedHash is not null && !env.IsProduction())
                        {
                            // Tables exist but model hash changed — drop and recreate in dev
                            Log.InfoCreatingMissingTables(logger, contextName);
                            await DropContextTablesAsync(dbContext, ct);
                            var creator = dbContext.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>();
                            await creator.CreateTablesAsync(ct);
                            await tracker.ClearAllSeedVersionsAsync(ct);
                        }
                    }
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

    /// <summary>
    ///     Drops all tables owned by the given <paramref name="dbContext"/> using EF model metadata.
    ///     Dev-only — used when no migration files exist but the model hash changed.
    /// </summary>
    private static async Task DropContextTablesAsync(DbContext dbContext, CancellationToken ct)
    {
        var entityTypes = dbContext.Model.GetEntityTypes();
        foreach (var entityType in entityTypes)
        {
            var tableName = entityType.GetTableName();
            var schema = entityType.GetSchema() ?? "public";
            if (tableName is null) continue;

            var sql = BuildDropTableSql(schema, tableName);

#pragma warning disable EF1003
            await dbContext.Database.ExecuteSqlRawAsync(sql, ct);
#pragma warning restore EF1003
        }
    }

    /// <summary>
    ///     Checks whether at least one table from the model exists in the database.
    ///     Used to detect stale model hashes left by broken previous runs.
    /// </summary>
    private static async Task<bool> ContextTablesExistAsync(DbContext dbContext, CancellationToken ct)
    {
        var firstEntity = dbContext.Model.GetEntityTypes().FirstOrDefault();
        if (firstEntity is null) return true;

        var tableName = firstEntity.GetTableName();
        var schema = firstEntity.GetSchema() ?? "public";
        if (tableName is null) return true;

        var sql = BuildTableExistsSql(schema, tableName);

#pragma warning disable EF1003
        // ExecuteSqlRaw cannot return scalar — use the underlying connection
        var conn = dbContext.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var result = await cmd.ExecuteScalarAsync(ct);
            return result is true or (bool)true;
        }
        finally
        {
            if (!wasOpen) await conn.CloseAsync();
        }
#pragma warning restore EF1003
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

    /// <summary>
    ///     Safely escapes a PostgreSQL string literal by wrapping in single quotes
    ///     and escaping any internal single quotes by doubling them.
    ///     Used for WHERE clause comparisons (e.g., information_schema queries).
    /// </summary>
    private static string EscapePostgreSqlStringLiteral(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be null or empty", nameof(value));
        // In PostgreSQL, single quotes inside string literals are escaped by doubling
        return "'" + value.Replace("'", "''") + "'";
    }

    /// <summary>
    ///     Safely escapes PostgreSQL identifiers (schema/table names) by wrapping in double quotes
    ///     and escaping any internal double quotes per PostgreSQL standard.
    ///     This prevents SQL injection attacks when dynamic identifiers are required.
    /// </summary>
    private static string EscapePostgreSqlIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("Identifier cannot be null or empty", nameof(identifier));
        // In PostgreSQL, identifiers in double quotes have internal quotes escaped by doubling
        return "\"" + identifier.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>
    ///     Builds a CREATE SCHEMA SQL command with properly escaped identifier.
    ///     Safe from SQL injection because identifier is escaped per PostgreSQL standard.
    /// </summary>
#pragma warning disable CA8305 // ExecuteSqlRaw - identifier is safely escaped
    private static string BuildCreateSchemaSql(string schemaName)
#pragma warning restore CA8305
    {
        var escaped = EscapePostgreSqlIdentifier(schemaName);
        return $"CREATE SCHEMA IF NOT EXISTS {escaped}";
    }

    /// <summary>
    ///     Builds a DROP TABLE SQL command with properly escaped identifiers.
    ///     Safe from SQL injection because identifiers are escaped per PostgreSQL standard.
    /// </summary>
#pragma warning disable CA8305 // ExecuteSqlRaw - identifiers are safely escaped
    private static string BuildDropTableSql(string schema, string tableName)
#pragma warning restore CA8305
    {
        var escapedSchema = EscapePostgreSqlIdentifier(schema);
        var escapedTableName = EscapePostgreSqlIdentifier(tableName);
        return $"DROP TABLE IF EXISTS {escapedSchema}.{escapedTableName} CASCADE";
    }

    /// <summary>
    ///     Builds a SELECT EXISTS query to check if a table exists.
    ///     Safe from SQL injection because values are escaped as PostgreSQL string literals.
    ///     Note: information_schema queries require single-quoted string literals, not double-quoted identifiers.
    /// </summary>
#pragma warning disable CA8305 // ExecuteSqlRaw - values are safely escaped as string literals
    private static string BuildTableExistsSql(string schema, string tableName)
#pragma warning restore CA8305
    {
        var escapedSchema = EscapePostgreSqlStringLiteral(schema);
        var escapedTableName = EscapePostgreSqlStringLiteral(tableName);
        return $"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = {escapedSchema} AND table_name = {escapedTableName})";
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

        [LoggerMessage((int)LogEventId.DbInitNoMigrationFiles + 1, LogLevel.Warning,
            "[{Context}] Model hash changed but no migration files — dropping and recreating tables (dev only)")]
        public static partial void InfoCreatingMissingTables(ILogger logger, string context);

        [LoggerMessage((int)LogEventId.DbInitNoMigrationFiles + 2, LogLevel.Warning,
            "[{Context}] Model hash matches but tables are missing — stale hash from broken previous run, forcing recreation")]
        public static partial void WarnStaleHash(ILogger logger, string context);


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
