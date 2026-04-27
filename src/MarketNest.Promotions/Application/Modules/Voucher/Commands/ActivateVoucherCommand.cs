namespace MarketNest.Promotions.Application;

public record ActivateVoucherCommand(Guid VoucherId, Guid RequestedByUserId) : ICommand<bool>;
