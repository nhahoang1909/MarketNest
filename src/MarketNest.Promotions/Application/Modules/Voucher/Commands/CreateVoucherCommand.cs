using MarketNest.Promotions.Domain;

namespace MarketNest.Promotions.Application;

public record CreateVoucherCommand(
    string Code,
    VoucherScope Scope,
    Guid? StoreId,
    Guid CreatedByUserId,
    VoucherDiscountType DiscountType,
    VoucherApplyFor ApplyFor,
    decimal DiscountValue,
    decimal? MaxDiscountCap,
    decimal? MinOrderValue,
    DateTime EffectiveDate,
    DateTime ExpiryDate,
    int? UsageLimit,
    int? UsageLimitPerUser) : ICommand<Guid>;
