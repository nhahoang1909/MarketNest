namespace MarketNest.Promotions.Domain;

public record VoucherUsageReversedEvent(Guid VoucherId, Guid OrderId) : IDomainEvent;
