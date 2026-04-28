using MarketNest.Promotions.Domain;

namespace MarketNest.Promotions.Application;

public record VoucherDto(
    Guid VoucherId,
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
    int? UsageLimitPerUser,
    int UsageCount,
    VoucherStatus Status,
    DateTimeOffset CreatedAt);
