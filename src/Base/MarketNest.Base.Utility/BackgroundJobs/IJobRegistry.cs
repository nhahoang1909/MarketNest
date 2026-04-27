namespace MarketNest.Base.Utility;

public interface IJobRegistry
{
    IReadOnlyCollection<JobDescriptor> GetJobs();

    JobDescriptor? FindByKey(string jobKey);
}
