namespace MarketNest.Promotions.Domain;

public record VoucherExpiredEvent(Guid VoucherId) : IDomainEvent;
