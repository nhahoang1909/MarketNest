namespace MarketNest.Base.Utility;

public sealed record JobDescriptor(
    string JobKey,
    string DisplayName,
    string OwningModule,
    JobType Type,
    string? Schedule,
    bool IsEnabled,
    bool IsRetryable,
    int MaxRetryCount,
    string? Description
);

public enum JobType
{
    Timer = 1,
    Batch = 2
}

public enum JobExecutionStatus
{
    Pending = 1,
    Running = 2,
    Succeeded = 3,
    Failed = 4,
    Cancelled = 5,
    Skipped = 6
}

