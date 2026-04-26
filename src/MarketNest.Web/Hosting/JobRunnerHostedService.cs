using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using MarketNest.Core.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MarketNest.Web.Hosting;

public class JobRunnerHostedService : BackgroundService
{
    private readonly IServiceProvider _provider;
    private readonly ILogger<JobRunnerHostedService> _logger;
    private readonly IJobRegistry _registry;
    private readonly IJobExecutionStore _store;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<string, byte> _running = new();

    public JobRunnerHostedService(IServiceProvider provider, ILogger<JobRunnerHostedService> logger, IJobRegistry registry, IJobExecutionStore store)
    {
        _provider = provider;
        _logger = logger;
        _registry = registry;
        _store = store;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("JobRunnerHostedService starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobs = _registry.GetJobs().Where(j => j.Type == JobType.Timer && j.IsEnabled).ToList();
                foreach (var descriptor in jobs)
                {
                    if (_running.ContainsKey(descriptor.JobKey)) continue; // already running

                    // fire-and-forget the job execution (but observe exceptions)
                    _ = Task.Run(() => RunOneAsync(descriptor, stoppingToken), stoppingToken);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while scheduling background jobs");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("JobRunnerHostedService stopping");
    }

    private async Task RunOneAsync(JobDescriptor descriptor, CancellationToken stoppingToken)
    {
        if (!_running.TryAdd(descriptor.JobKey, 0)) return;
        try
        {
            using var scope = _provider.CreateScope();
            var job = scope.ServiceProvider.GetServices<IBackgroundJob>().FirstOrDefault(j => j.Descriptor.JobKey == descriptor.JobKey);
            if (job is null)
            {
                _logger.LogWarning("No IBackgroundJob instance found for {JobKey}", descriptor.JobKey);
                return;
            }

            var ctx = new JobExecutionContext(Guid.Empty, descriptor.JobKey, null, JobTriggerSource.System, null, new Dictionary<string, string>());
            var executionId = await _store.CreateExecutionAsync(descriptor, ctx, stoppingToken);

            await _store.MarkRunningAsync(executionId, DateTime.UtcNow, stoppingToken);
            try
            {
                var runCtx = new JobExecutionContext(executionId, descriptor.JobKey, null, JobTriggerSource.System, null, new Dictionary<string, string>());
                await job.ExecuteAsync(runCtx, stoppingToken);
                await _store.MarkSucceededAsync(executionId, DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                await _store.MarkFailedAsync(executionId, DateTime.UtcNow, "Cancelled", null, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background job {JobKey} failed", descriptor.JobKey);
                await _store.MarkFailedAsync(executionId, DateTime.UtcNow, ex.Message, ex.ToString(), CancellationToken.None);
            }
        }
        finally
        {
            _running.TryRemove(descriptor.JobKey, out _);
        }
    }
}


