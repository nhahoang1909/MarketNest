using MarketNest.Base.Infrastructure;
using MarketNest.Promotions.Domain;

namespace MarketNest.Promotions.Application;

public partial class CreateVoucherHandler(
    IVoucherRepository repository,
    IAppLogger<CreateVoucherHandler> logger) : ICommandHandler<CreateVoucherCommand, Guid>
{
    public async Task<Result<Guid, Error>> Handle(CreateVoucherCommand request, CancellationToken cancellationToken)
    {
        Log.InfoStart(logger, request.Code);

        var code = new VoucherCode(request.Code);
        var voucher = Voucher.Create(
            code,
            request.Scope,
            request.StoreId,
            request.CreatedByUserId,
            request.DiscountType,
            request.ApplyFor,
            request.DiscountValue,
            request.MaxDiscountCap.HasValue ? new Money(request.MaxDiscountCap.Value, DomainConstants.Currencies.Default) : null,
            request.MinOrderValue.HasValue ? new Money(request.MinOrderValue.Value, DomainConstants.Currencies.Default) : null,
            request.EffectiveDate,
            request.ExpiryDate,
            request.UsageLimit,
            request.UsageLimitPerUser);

        repository.Add(voucher);
        await repository.SaveChangesAsync(cancellationToken);

        Log.InfoSuccess(logger, voucher.Id);
        return Result<Guid, Error>.Success(voucher.Id);
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.PromotionsCreateVoucherStart, LogLevel.Information,
            "CreateVoucher Start - Code={Code}")]
        public static partial void InfoStart(ILogger logger, string code);

        [LoggerMessage((int)LogEventId.PromotionsCreateVoucherSuccess, LogLevel.Information,
            "CreateVoucher Success - VoucherId={VoucherId}")]
        public static partial void InfoSuccess(ILogger logger, Guid voucherId);
    }
}
