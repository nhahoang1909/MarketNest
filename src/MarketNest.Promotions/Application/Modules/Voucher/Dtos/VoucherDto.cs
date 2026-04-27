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
    DateTime EffectiveDate,
    DateTime ExpiryDate,
    int? UsageLimit,
    int? UsageLimitPerUser,
    int UsageCount,
    VoucherStatus Status,
    DateTime CreatedAt);
