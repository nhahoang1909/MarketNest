using System.Collections.Concurrent;
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
            using IServiceScope scope = provider.CreateScope();
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
    }
}
