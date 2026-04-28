using System.Text.Json;
using MarketNest.Web.Infrastructure;
using MarketNest.Base.Utility;
using Npgsql;
using NpgsqlTypes;

namespace MarketNest.Web.BackgroundJobs;

public class NpgsqlJobExecutionStore : IJobExecutionStore
{
    private readonly string _connectionString;

    public NpgsqlJobExecutionStore(IConfiguration config)
    {
        _connectionString = config.GetConnectionString(AppConstants.DefaultConnectionStringName)
                            ?? throw new InvalidOperationException("Default connection string is not configured.");

#pragma warning disable MN004 // Constructor cannot be async — synchronous bootstrap of idempotent DDL is acceptable here
        EnsureTableAsync().GetAwaiter().GetResult();
#pragma warning restore MN004
    }

    private async Task EnsureTableAsync()
    {
        const string sql = @"
CREATE SCHEMA IF NOT EXISTS admin;
CREATE TABLE IF NOT EXISTS admin.job_executions (
  id uuid PRIMARY KEY,
  job_key text NOT NULL,
  job_type int NOT NULL,
  owning_module text NOT NULL,
  status int NOT NULL,
  trigger_source int NOT NULL,
  triggered_by_user_id uuid NULL,
  retry_of_execution_id uuid NULL,
  started_at_utc timestamptz NULL,
  finished_at_utc timestamptz NULL,
  duration_ms int NULL,
  error_message text NULL,
  error_details text NULL,
  parameters_json jsonb NULL,
  created_at_utc timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS idx_job_executions_job_key ON admin.job_executions (job_key);
CREATE INDEX IF NOT EXISTS idx_job_executions_status ON admin.job_executions (status);
";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<Guid> CreateExecutionAsync(JobDescriptor descriptor, JobExecutionContext context,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO admin.job_executions
  (id, job_key, job_type, owning_module, status, trigger_source, triggered_by_user_id, retry_of_execution_id, parameters_json, created_at_utc)
  VALUES (@id, @job_key, @job_type, @owning_module, @status, @trigger_source, @triggered_by_user_id, @retry_of_execution_id, @parameters_json, now())";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@job_key", descriptor.JobKey);
        cmd.Parameters.AddWithValue("@job_type", (int)descriptor.Type);
        cmd.Parameters.AddWithValue("@owning_module", descriptor.OwningModule);
        cmd.Parameters.AddWithValue("@status", (int)JobExecutionStatus.Pending);
        cmd.Parameters.AddWithValue("@trigger_source", (int)context.TriggerSource);
        cmd.Parameters.AddWithValue("@triggered_by_user_id",
            context.TriggeredByUserId == null ? (object)DBNull.Value : context.TriggeredByUserId);
        cmd.Parameters.AddWithValue("@retry_of_execution_id",
            context.RetryOfExecutionId == null ? (object)DBNull.Value : context.RetryOfExecutionId);
        string paramsJson = JsonSerializer.Serialize(context.Parameters);
        cmd.Parameters.AddWithValue("@parameters_json", NpgsqlDbType.Jsonb, paramsJson);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        return id;
    }

    public async Task MarkRunningAsync(Guid executionId, DateTime startedAtUtc, CancellationToken cancellationToken)
    {
        const string sql =
            @"UPDATE admin.job_executions SET status = @status, started_at_utc = @started WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@status", (int)JobExecutionStatus.Running);
        cmd.Parameters.AddWithValue("@started", startedAtUtc);
        cmd.Parameters.AddWithValue("@id", executionId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkSucceededAsync(Guid executionId, DateTime finishedAtUtc, CancellationToken cancellationToken)
    {
        const string sql =
            @"UPDATE admin.job_executions SET status = @status, finished_at_utc = @finished, duration_ms = EXTRACT(EPOCH FROM (@finished - started_at_utc))::int WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@status", (int)JobExecutionStatus.Succeeded);
        cmd.Parameters.AddWithValue("@finished", finishedAtUtc);
        cmd.Parameters.AddWithValue("@id", executionId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(Guid executionId, DateTime finishedAtUtc, string errorMessage,
        string? errorDetails, CancellationToken cancellationToken)
    {
        const string sql =
            @"UPDATE admin.job_executions SET status = @status, finished_at_utc = @finished, duration_ms = EXTRACT(EPOCH FROM (@finished - started_at_utc))::int, error_message = @err, error_details = @errd WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@status", (int)JobExecutionStatus.Failed);
        cmd.Parameters.AddWithValue("@finished", finishedAtUtc);
        cmd.Parameters.AddWithValue("@err", errorMessage ?? string.Empty);
        cmd.Parameters.AddWithValue("@errd", (object?)errorDetails ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id", executionId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
