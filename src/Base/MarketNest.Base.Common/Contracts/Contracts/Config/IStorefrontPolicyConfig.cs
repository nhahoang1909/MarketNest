namespace MarketNest.Base.Common;

/// <summary>
///     Read contract for Storefront Policy configuration.
///     Implemented by <c>StorefrontPolicyConfigService</c> in the Catalog module.
/// </summary>
public interface IStorefrontPolicyConfig
{
    /// <summary>Maximum number of active products per storefront. Default: 500.</summary>
    int MaxProductsPerStorefront { get; }

    /// <summary>Maximum number of images per product listing. Default: 5.</summary>
    int MaxImagesPerProduct { get; }

    /// <summary>Maximum number of variants (SKUs) per product. Default: 50.</summary>
    int MaxProductVariantsPerProduct { get; }

    /// <summary>Whether email verification is required before a storefront can be activated. Default: true.</summary>
    bool RequireEmailVerificationToActivate { get; }
}

