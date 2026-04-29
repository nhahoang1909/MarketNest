using MarketNest.Base.Utility;
using MarketNest.Catalog.Application;
using Microsoft.EntityFrameworkCore;
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
    private const string ConnectionStringName = "DefaultConnection";

    public static IServiceCollection AddCatalogModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<CatalogDbContext>(opts => opts.UseNpgsql(connectionString));
        services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<CatalogDbContext>());

        services.AddSingleton<StorefrontPolicyConfigService>();
        services.AddSingleton<IStorefrontPolicyConfig>(sp =>
            sp.GetRequiredService<StorefrontPolicyConfigService>());
        services.AddSingleton<IStorefrontPolicyConfigWriter>(sp =>
            sp.GetRequiredService<StorefrontPolicyConfigService>());

        // Repository
        services.AddScoped<IVariantRepository, VariantRepository>();

        // Background job — scoped so it can receive IVariantRepository
        services.AddScoped<IBackgroundJob, ExpireSalesJob>();

        return services;
    }
}

