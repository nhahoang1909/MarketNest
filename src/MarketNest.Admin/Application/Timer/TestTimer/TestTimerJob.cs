using MarketNest.Base.Infrastructure;
using MarketNest.Base.Utility;

namespace MarketNest.Admin.Application;

public class TestTimerJob(IAppLogger<TestTimerJob> logger) : IBackgroundJob
{
    private const string JobKeyValue = "admin.test.timer";
    private const string ModuleName = "Admin";

    public JobDescriptor Descriptor { get; } = new(
        JobKeyValue,
        "Admin demo timer job",
        ModuleName,
        JobType.Timer,
        null,
        true,
        false,
        0,
        "A demo job that logs a message and completes.");

    public Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken = default)
    {
        logger.Info("TestTimerJob executed: {ExecutionId}", context.ExecutionId);
        return Task.CompletedTask;
    }
}
