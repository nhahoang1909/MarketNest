namespace MarketNest.Base.Utility;

public interface IJobExecutionStore
{
    Task<Guid> CreateExecutionAsync(JobDescriptor descriptor, JobExecutionContext context, CancellationToken cancellationToken);

    Task MarkRunningAsync(Guid executionId, DateTime startedAtUtc, CancellationToken cancellationToken);

    Task MarkSucceededAsync(Guid executionId, DateTime finishedAtUtc, CancellationToken cancellationToken);

    Task MarkFailedAsync(Guid executionId, DateTime finishedAtUtc, string errorMessage, string? errorDetails, CancellationToken cancellationToken);
}

