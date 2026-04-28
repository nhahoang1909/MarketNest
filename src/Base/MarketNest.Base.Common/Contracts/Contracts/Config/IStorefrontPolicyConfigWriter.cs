namespace MarketNest.Base.Common;

/// <summary>
///     Write contract for Storefront Policy configuration.
///     Called by Admin module only; implemented by Catalog module.
/// </summary>
public interface IStorefrontPolicyConfigWriter
{
    Task<Result<Unit, Error>> UpdateAsync(UpdateStorefrontPolicyRequest request, CancellationToken ct = default);
}

/// <summary>Input for updating storefront policy limits.</summary>
public record UpdateStorefrontPolicyRequest(
    int MaxProductsPerStorefront,
    int MaxImagesPerProduct,
    int MaxProductVariantsPerProduct,
    bool RequireEmailVerificationToActivate);

