namespace MarketNest.Promotions.Application;

public record PauseVoucherCommand(Guid VoucherId, Guid RequestedByUserId) : ICommand<bool>;
