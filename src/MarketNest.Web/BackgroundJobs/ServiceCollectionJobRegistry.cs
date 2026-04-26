using MarketNest.Core.BackgroundJobs;

namespace MarketNest.Web.BackgroundJobs;

public class ServiceCollectionJobRegistry : IJobRegistry
{
    private readonly IServiceProvider _provider;
    private IReadOnlyCollection<JobDescriptor>? _cache;

    public ServiceCollectionJobRegistry(IServiceProvider provider)
    {
        _provider = provider;
    }

    public IReadOnlyCollection<JobDescriptor> GetJobs()
    {
        if (_cache is not null) return _cache;
        using var scope = _provider.CreateScope();
        var jobs = scope.ServiceProvider.GetServices<IBackgroundJob>()
            .Select(j => j.Descriptor)
            .ToArray();
        _cache = Array.AsReadOnly(jobs);
        return _cache;
    }

    public JobDescriptor? FindByKey(string jobKey)
        => GetJobs().FirstOrDefault(j => j.JobKey == jobKey);
}

