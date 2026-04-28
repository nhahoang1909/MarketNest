using MarketNest.Promotions.Domain;

namespace MarketNest.Promotions.Infrastructure;

public record CreateVoucherRequest(
    string Code,
    VoucherScope Scope,
    Guid? StoreId,
    VoucherDiscountType DiscountType,
    VoucherApplyFor ApplyFor,
    decimal DiscountValue,
    decimal? MaxDiscountCap,
    decimal? MinOrderValue,
    DateTimeOffset EffectiveDate,
    DateTimeOffset ExpiryDate,
    int? UsageLimit,
    int? UsageLimitPerUser);
