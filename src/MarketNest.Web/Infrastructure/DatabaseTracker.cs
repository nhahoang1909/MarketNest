using System.Data;
using MarketNest.Core.Logging;
using Npgsql;

namespace MarketNest.Web.Infrastructure;

/// <summary>
/// Manages the <c>_system.__auto_migration_history</c> and <c>_system.__seed_history</c> tables
/// via raw SQL (independent of any module DbContext to avoid circular dependencies).
///
/// These tables live in the <c>_system</c> schema — a shared schema not owned by any module.
/// </summary>
public sealed class DatabaseTracker(
    IConfiguration configuration,
    IAppLogger<DatabaseTracker> logger)
{
    private const string Schema = "_system";
    private const string MigrationTable = "__auto_migration_history";
    private const string SeedTable = "__seed_history";

    // PostgreSQL advisory lock key — prevents concurrent instances from racing
    private const long AdvisoryLockId = 0x4D61726B65744E73; // "MarketNs" in hex

    private string ConnectionString =>
        configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

    // ─── Bootstrap ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates the <c>_system</c> schema and both tracking tables if they don't exist.
    /// Safe to call multiple times (idempotent).
    /// </summary>
    public async Task EnsureTrackingTablesExistAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync(ct);

        const string sql = $"""
            CREATE SCHEMA IF NOT EXISTS {Schema};

            CREATE TABLE IF NOT EXISTS {Schema}.{MigrationTable} (
                id              SERIAL PRIMARY KEY,
                context_name    VARCHAR(256) NOT NULL,
                model_hash      VARCHAR(64)  NOT NULL,
                applied_at_utc  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
            );

            CREATE TABLE IF NOT EXISTS {Schema}.{SeedTable} (
                id              SERIAL PRIMARY KEY,
                seeder_name     VARCHAR(512) NOT NULL,
                version         VARCHAR(64)  NOT NULL,
                applied_at_utc  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
            );

            -- Unique indexes for fast lookups
            CREATE UNIQUE INDEX IF NOT EXISTS ix_migration_context
                ON {Schema}.{MigrationTable} (context_name);

            CREATE UNIQUE INDEX IF NOT EXISTS ix_seed_seeder
                ON {Schema}.{SeedTable} (seeder_name);
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);

        logger.Debug("Tracking tables ensured in schema '{Schema}'", Schema);
    }

    // ─── Advisory Lock ────────────────────────────────────────────────────

    /// <summary>
    /// Acquires a PostgreSQL session-level advisory lock. Returns the connection
    /// (caller must dispose it to release the lock).
    /// </summary>
    public async Task<NpgsqlConnection> AcquireAdvisoryLockAsync(CancellationToken ct = default)
    {
        var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand($"SELECT pg_advisory_lock({AdvisoryLockId})", conn);
        await cmd.ExecuteNonQueryAsync(ct);

        logger.Debug("Advisory lock {LockId} acquired", AdvisoryLockId);
        return conn;
    }

    /// <summary>Releases the advisory lock and closes the connection.</summary>
    public async Task ReleaseAdvisoryLockAsync(NpgsqlConnection conn)
    {
        try
        {
            if (conn.State == ConnectionState.Open)
            {
                await using var cmd = new NpgsqlCommand($"SELECT pg_advisory_unlock({AdvisoryLockId})", conn);
                await cmd.ExecuteNonQueryAsync();

                logger.Debug("Advisory lock {LockId} released", AdvisoryLockId);
            }
        }
        finally
        {
            await conn.DisposeAsync();
        }
    }

    // ─── Migration History ────────────────────────────────────────────────

    /// <summary>Returns the last stored model hash for a context, or <c>null</c> if first run.</summary>
    public async Task<string?> GetLastModelHashAsync(string contextName, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync(ct);

        const string sql = $"""
            SELECT model_hash FROM {Schema}.{MigrationTable}
            WHERE context_name = @ctx
            LIMIT 1
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("ctx", contextName);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }

    /// <summary>Upserts the model hash for a context after a successful migration.</summary>
    public async Task SaveModelHashAsync(string contextName, string modelHash, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync(ct);

        const string sql = $"""
            INSERT INTO {Schema}.{MigrationTable} (context_name, model_hash, applied_at_utc)
            VALUES (@ctx, @hash, NOW())
            ON CONFLICT (context_name)
            DO UPDATE SET model_hash = @hash, applied_at_utc = NOW()
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("ctx", contextName);
        cmd.Parameters.AddWithValue("hash", modelHash);
        await cmd.ExecuteNonQueryAsync(ct);

        logger.Debug("Model hash saved for context '{Context}': {Hash}", contextName, modelHash[..12] + "…");
    }

    // ─── Seed History ─────────────────────────────────────────────────────

    /// <summary>Returns the last stored version for a seeder, or <c>null</c> if never run.</summary>
    public async Task<string?> GetLastSeedVersionAsync(string seederName, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync(ct);

        const string sql = $"""
            SELECT version FROM {Schema}.{SeedTable}
            WHERE seeder_name = @name
            LIMIT 1
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", seederName);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }

    /// <summary>Upserts the seeder version after a successful seed run.</summary>
    public async Task SaveSeedVersionAsync(string seederName, string version, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync(ct);

        const string sql = $"""
            INSERT INTO {Schema}.{SeedTable} (seeder_name, version, applied_at_utc)
            VALUES (@name, @ver, NOW())
            ON CONFLICT (seeder_name)
            DO UPDATE SET version = @ver, applied_at_utc = NOW()
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", seederName);
        cmd.Parameters.AddWithValue("ver", version);
        await cmd.ExecuteNonQueryAsync(ct);

        logger.Debug("Seed version saved for '{Seeder}': {Version}", seederName, version);
    }
}

