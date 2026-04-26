using MarketNest.Core.BackgroundJobs;
using MarketNest.Core.Logging;
using Microsoft.Extensions.Logging;

namespace MarketNest.Admin.Application;

public class TestTimerJob(IAppLogger<TestTimerJob> logger) : IBackgroundJob
{
    public JobDescriptor Descriptor { get; } = new(
        JobKey: "admin.test.timer",
        DisplayName: "Admin demo timer job",
        OwningModule: "Admin",
        Type: JobType.Timer,
        Schedule: null,
        IsEnabled: true,
        IsRetryable: false,
        MaxRetryCount: 0,
        Description: "A demo job that logs a message and completes.");

    public Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.Info("TestTimerJob executed: {ExecutionId}", context.ExecutionId);
        return Task.CompletedTask;
    }
}
