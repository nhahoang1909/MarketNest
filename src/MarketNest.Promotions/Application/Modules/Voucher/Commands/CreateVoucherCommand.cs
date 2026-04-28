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
    DateTimeOffset EffectiveDate,
    DateTimeOffset ExpiryDate,
    int? UsageLimit,
    int? UsageLimitPerUser) : ICommand<Guid>;
