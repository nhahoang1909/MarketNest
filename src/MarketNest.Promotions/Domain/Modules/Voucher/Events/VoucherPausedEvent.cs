namespace MarketNest.Promotions.Domain;

public record VoucherPausedEvent(Guid VoucherId) : IDomainEvent;
