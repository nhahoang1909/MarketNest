using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MarketNest.Core.BackgroundJobs;

namespace MarketNest.Web.BackgroundJobs.Test;

public class TestTimerJob : IBackgroundJob
{
    public JobDescriptor Descriptor { get; } = new(
        JobKey: "test.demo.timer",
        DisplayName: "Demo test timer job",
        OwningModule: "Web",
        Type: JobType.Timer,
        Schedule: null,
        IsEnabled: true,
        IsRetryable: false,
        MaxRetryCount: 0,
        Description: "A small demo job that logs a message and completes.");

    private readonly ILogger<TestTimerJob> _logger;

    public TestTimerJob(ILogger<TestTimerJob> logger)
    {
        _logger = logger;
    }

    public Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("TestTimerJob executed: {ExecutionId}", context.ExecutionId);
        return Task.CompletedTask;
    }
}


