namespace MarketNest.Promotions.Domain;

public record VoucherActivatedEvent(Guid VoucherId) : IDomainEvent;
