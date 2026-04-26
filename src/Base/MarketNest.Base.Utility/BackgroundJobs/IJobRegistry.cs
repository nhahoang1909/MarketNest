namespace MarketNest.Core.BackgroundJobs;

public interface IJobRegistry
{
    IReadOnlyCollection<JobDescriptor> GetJobs();

    JobDescriptor? FindByKey(string jobKey);
}

