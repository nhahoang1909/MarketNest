namespace MarketNest.Promotions.Domain;

public record VoucherAppliedEvent(
    Guid VoucherId,
    Guid OrderId,
    Guid UserId,
    Money DiscountApplied) : IDomainEvent;
