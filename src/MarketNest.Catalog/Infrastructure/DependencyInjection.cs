using Microsoft.Extensions.Configuration;

namespace MarketNest.Catalog.Infrastructure;

/// <summary>
///     In-memory defaults implementation of storefront policy config.
///     Phase 2: replace with DB-backed implementation and real DbContext.
/// </summary>
internal sealed class StorefrontPolicyConfigService : IStorefrontPolicyConfig, IStorefrontPolicyConfigWriter
{
    public int MaxProductsPerStorefront { get; private set; } = 500;
    public int MaxImagesPerProduct { get; private set; } = 5;
    public int MaxProductVariantsPerProduct { get; private set; } = 50;
    public bool RequireEmailVerificationToActivate { get; private set; } = true;

    public Task<Result<Unit, Error>> UpdateAsync(
        UpdateStorefrontPolicyRequest request, CancellationToken ct = default)
    {
        MaxProductsPerStorefront = request.MaxProductsPerStorefront;
        MaxImagesPerProduct = request.MaxImagesPerProduct;
        MaxProductVariantsPerProduct = request.MaxProductVariantsPerProduct;
        RequireEmailVerificationToActivate = request.RequireEmailVerificationToActivate;
        return Task.FromResult(Result.Success());
    }
}

/// <summary>DI registration for the Catalog module.</summary>
public static class CatalogServiceExtensions
{
    public static IServiceCollection AddCatalogModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<StorefrontPolicyConfigService>();
        services.AddSingleton<IStorefrontPolicyConfig>(sp =>
            sp.GetRequiredService<StorefrontPolicyConfigService>());
        services.AddSingleton<IStorefrontPolicyConfigWriter>(sp =>
            sp.GetRequiredService<StorefrontPolicyConfigService>());

        return services;
    }
}

