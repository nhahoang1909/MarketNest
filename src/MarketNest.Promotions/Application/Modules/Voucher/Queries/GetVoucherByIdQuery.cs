namespace MarketNest.Promotions.Application;

public record GetVoucherByIdQuery(Guid VoucherId) : IQuery<VoucherDto?>;
