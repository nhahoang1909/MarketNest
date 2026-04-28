namespace MarketNest.Promotions.Domain;

public record VoucherCreatedEvent(Guid VoucherId, string Code) : IDomainEvent;
