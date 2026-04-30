using Microsoft.Extensions.Configuration;

namespace MarketNest.Reviews.Infrastructure;

/// <summary>
///     In-memory defaults implementation of review policy config.
///     Phase 2: replace with DB-backed implementation.
/// </summary>
internal sealed class ReviewPolicyConfigService : IReviewPolicyConfig, IReviewPolicyConfigWriter
{
    public bool AllowReviewAfterDisputedOrder { get; private set; }
    public int ReviewEditWindowHours { get; private set; } = 24;
    public int MaxReviewBodyLength { get; private set; } = 1000;

    public Task<Result<Unit, Error>> UpdateAsync(
        UpdateReviewPolicyRequest request, CancellationToken ct = default)
    {
        AllowReviewAfterDisputedOrder = request.AllowReviewAfterDisputedOrder;
        ReviewEditWindowHours = request.ReviewEditWindowHours;
        MaxReviewBodyLength = request.MaxReviewBodyLength;
        return Task.FromResult(Result.Success());
    }
}

/// <summary>DI registration for the Reviews module.</summary>
public static class ReviewsServiceExtensions
{
    public static IServiceCollection AddReviewsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<ReviewPolicyConfigService>();
        services.AddSingleton<IReviewPolicyConfig>(sp =>
            sp.GetRequiredService<ReviewPolicyConfigService>());
        services.AddSingleton<IReviewPolicyConfigWriter>(sp =>
            sp.GetRequiredService<ReviewPolicyConfigService>());

        return services;
    }
}

