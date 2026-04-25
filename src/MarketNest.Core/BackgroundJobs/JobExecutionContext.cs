namespace MarketNest.Core.BackgroundJobs;

public sealed record JobExecutionContext(
    Guid ExecutionId,
    string JobKey,
    Guid? TriggeredByUserId,
    JobTriggerSource TriggerSource,
    Guid? RetryOfExecutionId,
    IReadOnlyDictionary<string, string> Parameters
);

public enum JobTriggerSource
{
    System = 1,
    Admin = 2,
    Retry = 3
}

