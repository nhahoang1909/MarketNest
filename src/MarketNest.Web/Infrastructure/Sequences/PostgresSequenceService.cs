using System.Collections.Concurrent;
using MarketNest.Base.Common;
using Npgsql;

namespace MarketNest.Web.Infrastructure;

/// <summary>
/// PostgreSQL-backed implementation of <see cref="ISequenceService"/>.
/// Uses period-scoped sequence names to achieve deadlock-free, race-condition-safe
/// running number generation with automatic monthly/yearly reset.
///
/// Registered as Singleton — the in-process <c>_provisionedSequences</c> cache
/// must be shared across all requests to avoid redundant DDL per request.
/// </summary>
internal sealed class PostgresSequenceService : ISequenceService
{
    private readonly string _connectionString;

    /// <summary>
    /// In-process cache: tracks which sequence names have been provisioned
    /// in the current app lifetime. Avoids redundant CREATE IF NOT EXISTS DDL
    /// on every call within the same period.
    /// </summary>
    private readonly ConcurrentDictionary<string, bool> _provisionedSequences = new();

    public PostgresSequenceService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString(AppConstants.DefaultConnectionStringName)
                            ?? throw new InvalidOperationException(
                                $"Connection string '{AppConstants.DefaultConnectionStringName}' is not configured.");
    }

    public async Task<string> NextFormattedAsync(
        SequenceDescriptor descriptor,
        CancellationToken ct = default)
    {
        var asOf = DateTimeOffset.UtcNow;
        var seqName = descriptor.GetSequenceName(asOf);

        await EnsureSequenceExistsAsync(seqName, ct);

        var value = await NextValAsync(seqName, ct);

        return descriptor.Format(value, asOf);
    }

    public async Task<long> NextValueAsync(
        SequenceDescriptor descriptor,
        CancellationToken ct = default)
    {
        var asOf = DateTimeOffset.UtcNow;
        var seqName = descriptor.GetSequenceName(asOf);

        await EnsureSequenceExistsAsync(seqName, ct);

        return await NextValAsync(seqName, ct);
    }

    public async Task<IReadOnlyList<string>> ListSequenceNamesAsync(
        SequenceDescriptor descriptor,
        string periodKeyPrefix,
        CancellationToken ct = default)
    {
        var namePattern = $"seq_{descriptor.BaseName}_{periodKeyPrefix}%";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT schemaname || '.' || sequencename
            FROM pg_sequences
            WHERE schemaname = @schema
              AND sequencename LIKE @pattern
            ORDER BY sequencename;
            """, conn);

        cmd.Parameters.AddWithValue("@schema", descriptor.Schema);
        cmd.Parameters.AddWithValue("@pattern", namePattern);

        var results = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(reader.GetString(0));

        return results;
    }

    public async Task DropSequenceAsync(string schemaQualifiedName, CancellationToken ct = default)
    {
        // Validate: schema-qualified name must match expected pattern (schema.seq_baseName_periodKey)
        // to prevent SQL injection via crafted sequence names.
        if (!IsValidSequenceName(schemaQualifiedName))
            throw new ArgumentException(
                $"Invalid schema-qualified sequence name: '{schemaQualifiedName}'", nameof(schemaQualifiedName));

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            $"DROP SEQUENCE IF EXISTS {schemaQualifiedName}", conn);
        await cmd.ExecuteNonQueryAsync(ct);

        _provisionedSequences.TryRemove(schemaQualifiedName, out _);
    }

    // ─── Private ──────────────────────────────────────────────────────────────

    private async Task EnsureSequenceExistsAsync(string seqName, CancellationToken ct)
    {
        // Fast path: already provisioned in this app lifetime
        if (_provisionedSequences.ContainsKey(seqName))
            return;

        // Slow path: first call for this period.
        // CREATE SEQUENCE IF NOT EXISTS is safe under concurrent cold-start:
        // PostgreSQL serializes DDL at the catalog lock level.
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            $"""
            CREATE SEQUENCE IF NOT EXISTS {seqName}
                START WITH 1
                INCREMENT BY 1
                NO MINVALUE
                NO MAXVALUE
                CACHE 1;
            """, conn);
        await cmd.ExecuteNonQueryAsync(ct);

        _provisionedSequences[seqName] = true;
    }

    private async Task<long> NextValAsync(string seqName, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        // seqName is always machine-generated (schema.seq_baseName_periodKey),
        // never user input — safe to embed in query text.
        await using var cmd = new NpgsqlCommand($"SELECT NEXTVAL('{seqName}')", conn);
        var raw = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(raw, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Validates that a schema-qualified sequence name matches the expected format:
    /// {schema}.seq_{baseName}_{periodKey}
    /// Only lowercase letters, digits, underscores, and a single dot separator.
    /// </summary>
    private static bool IsValidSequenceName(string name)
        => System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-z][a-z0-9_]*\.seq_[a-z0-9_]+$");
}

