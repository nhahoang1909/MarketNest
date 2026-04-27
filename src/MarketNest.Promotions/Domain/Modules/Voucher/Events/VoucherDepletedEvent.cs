namespace MarketNest.Promotions.Domain;

public record VoucherDepletedEvent(Guid VoucherId) : IDomainEvent;
