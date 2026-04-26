using MarketNest.Core.BackgroundJobs;
using MarketNest.Core.Logging;

namespace MarketNest.Admin.Application;

public class TestTimerJob(IAppLogger<TestTimerJob> logger) : IBackgroundJob
{
    private const string JobKeyValue = "admin.test.timer";
    private const string ModuleName = "Admin";

    public JobDescriptor Descriptor { get; } = new(
        JobKey: JobKeyValue,
        DisplayName: "Admin demo timer job",
        OwningModule: ModuleName,
        Type: JobType.Timer,
        Schedule: null,
        IsEnabled: true,
        IsRetryable: false,
        MaxRetryCount: 0,
        Description: "A demo job that logs a message and completes.");

    public Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken = default)
    {
        logger.Info("TestTimerJob executed: {ExecutionId}", context.ExecutionId);
        return Task.CompletedTask;
    }
}
