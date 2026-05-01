using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using MarketNest.Base.Infrastructure;
using MarketNest.Base.Utility;

namespace MarketNest.Web.Hosting;

// NOTE: BackgroundJobRunner is registered as a singleton IHostedService. It must NOT directly
// depend on scoped services. Resolve IJobExecutionStore from the per-job IServiceScope below.
public partial class BackgroundJobRunner(
    IServiceProvider provider,
    IAppLogger<BackgroundJobRunner> logger,
    IJobRegistry registry) : BackgroundService
{
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<string, byte> _running = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.InfoStarting(logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobs = registry.GetJobs().Where(j => j.Type == JobType.Timer && j.IsEnabled).ToList();
                foreach (var descriptor in jobs)
                {
                    if (_running.ContainsKey(descriptor.JobKey)) continue; // already running

                    // fire-and-forget the job execution (but observe exceptions)
                    _ = Task.Run(() => RunOneAsync(descriptor, stoppingToken), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.ErrorScheduling(logger, ex);
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        Log.InfoStopping(logger);
    }

    private async Task RunOneAsync(JobDescriptor descriptor, CancellationToken stoppingToken)
    {
        if (!_running.TryAdd(descriptor.JobKey, 0)) return;
        try
        {
            // Use CreateAsyncScope so IAsyncDisposable scoped services (e.g. IUnitOfWork) are
            // properly disposed via DisposeAsync — using CreateScope() would throw if the scope
            // contains async-only disposables (see bugs.md 2026-05-01).
            await using var scope = provider.CreateAsyncScope();
            var job = scope.ServiceProvider.GetServices<IBackgroundJob>()
                .FirstOrDefault(j => j.Descriptor.JobKey == descriptor.JobKey);
            if (job is null)
            {
                Log.WarnJobNotFound(logger, descriptor.JobKey);
                return;
            }

            var ctx = new JobExecutionContext(Guid.Empty, descriptor.JobKey, null, JobTriggerSource.System, null,
                new Dictionary<string, string>());
            // Resolve the scoped IJobExecutionStore from the newly created scope so we don't capture
            // a scoped service in this singleton hosted service.
            var store = scope.ServiceProvider.GetRequiredService<IJobExecutionStore>();
            var executionId = await store.CreateExecutionAsync(descriptor, ctx, stoppingToken);

            await store.MarkRunningAsync(executionId, DateTime.UtcNow, stoppingToken);
            try
            {
                var runCtx = new JobExecutionContext(executionId, descriptor.JobKey, null,
                    JobTriggerSource.System, null, new Dictionary<string, string>());

                // If the job class carries [BackgroundJobTransaction], the runner owns the full
                // UoW transaction lifecycle (Begin → Commit → Dispatch / Rollback / Dispose).
                // Jobs using this attribute must NOT inject IUnitOfWork themselves.
                var txAttr = job.GetType().GetCustomAttribute<BackgroundJobTransactionAttribute>();
                if (txAttr is not null)
                    await RunWithTransactionAsync(job, runCtx, scope.ServiceProvider, txAttr, stoppingToken);
                else
                    await job.ExecuteAsync(runCtx, stoppingToken);

                await store.MarkSucceededAsync(executionId, DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                await store.MarkFailedAsync(executionId, DateTime.UtcNow, "Cancelled", null, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Log.ErrorJobFailed(logger, descriptor.JobKey, ex);
                await store.MarkFailedAsync(executionId, DateTime.UtcNow, ex.Message, ex.ToString(),
                    CancellationToken.None);
            }
        }
        finally
        {
            _running.TryRemove(descriptor.JobKey, out _);
        }
    }

    /// <summary>
    ///     Wraps a job's <see cref="IBackgroundJob.ExecuteAsync"/> in the full UoW transaction
    ///     lifecycle when the job class carries <see cref="BackgroundJobTransactionAttribute"/>.
    ///     <list type="bullet">
    ///         <item>Begin TX → Execute → CommitAsync → CommitTransactionAsync → DispatchPostCommitEvents</item>
    ///         <item>On any exception → RollbackAsync (using <see cref="CancellationToken.None"/> so the
    ///               rollback is never skipped due to cancellation)</item>
    ///         <item>Always → DisposeAsync</item>
    ///     </list>
    /// </summary>
    private async Task RunWithTransactionAsync(
        IBackgroundJob job,
        JobExecutionContext ctx,
        IServiceProvider services,
        BackgroundJobTransactionAttribute txAttr,
        CancellationToken ct)
    {
        var uow = services.GetRequiredService<IUnitOfWork>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(txAttr.TimeoutSeconds));

        Log.InfoTxBegin(logger, job.Descriptor.JobKey, txAttr.IsolationLevel);
        try
        {
            await uow.BeginTransactionAsync(txAttr.IsolationLevel, cts.Token);
            await job.ExecuteAsync(ctx, cts.Token);
            await uow.CommitAsync(cts.Token);
            await uow.CommitTransactionAsync(cts.Token);
            Log.InfoTxCommitted(logger, job.Descriptor.JobKey);

            // DispatchPostCommitEventsAsync uses the original ct — post-commit dispatch must
            // not be cancelled by the job timeout (the DB is already committed at this point).
            await uow.DispatchPostCommitEventsAsync(ct);
        }
        catch (Exception)
        {
            // CancellationToken.None: ensure rollback always runs even if ct is already cancelled.
            await uow.RollbackAsync(CancellationToken.None);
            Log.WarnTxRolledBack(logger, job.Descriptor.JobKey);
            throw;
        }
        finally
        {
            await uow.DisposeAsync();
        }
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.JobRunnerStarting, LogLevel.Information,
            "BackgroundJobRunner starting")]
        public static partial void InfoStarting(ILogger logger);

        [LoggerMessage((int)LogEventId.JobRunnerStopping, LogLevel.Information,
            "BackgroundJobRunner stopping")]
        public static partial void InfoStopping(ILogger logger);

        [LoggerMessage((int)LogEventId.JobRunnerJobFailed - 1, LogLevel.Warning,
            "No IBackgroundJob instance found for {JobKey}")]
        public static partial void WarnJobNotFound(ILogger logger, string jobKey);

        [LoggerMessage((int)LogEventId.JobRunnerJobFailed, LogLevel.Error,
            "Background job {JobKey} failed")]
        public static partial void ErrorJobFailed(ILogger logger, string jobKey, Exception ex);

        [LoggerMessage((int)LogEventId.JobRunnerJobFailed + 1, LogLevel.Error,
            "Error while scheduling background jobs")]
        public static partial void ErrorScheduling(ILogger logger, Exception ex);

        [LoggerMessage((int)LogEventId.JobRunnerTxBegin, LogLevel.Debug,
            "Background job TX BEGIN — JobKey={JobKey} Isolation={Isolation}")]
        public static partial void InfoTxBegin(ILogger logger, string jobKey, IsolationLevel isolation);

        [LoggerMessage((int)LogEventId.JobRunnerTxCommitted, LogLevel.Debug,
            "Background job TX COMMITTED — JobKey={JobKey}")]
        public static partial void InfoTxCommitted(ILogger logger, string jobKey);

        [LoggerMessage((int)LogEventId.JobRunnerTxRolledBack, LogLevel.Warning,
            "Background job TX ROLLED BACK — JobKey={JobKey}")]
        public static partial void WarnTxRolledBack(ILogger logger, string jobKey);
    }
}
