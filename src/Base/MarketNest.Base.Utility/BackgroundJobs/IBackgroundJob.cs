namespace MarketNest.Base.Utility;

public interface IBackgroundJob
{
    JobDescriptor Descriptor { get; }

    Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken = default);
}

