using MarketNest.Base.Infrastructure;

namespace MarketNest.Promotions.Application;

public partial class ActivateVoucherHandler(
    IVoucherRepository repository,
    IAppLogger<ActivateVoucherHandler> logger) : ICommandHandler<ActivateVoucherCommand, bool>
{
    public async Task<Result<bool, Error>> Handle(ActivateVoucherCommand request, CancellationToken cancellationToken)
    {
        Log.InfoStart(logger, request.VoucherId);

        var voucher = await repository.GetByKeyAsync(request.VoucherId, cancellationToken);
        Result<bool, Error> result = voucher.Activate();
        if (!result.IsSuccess) return result;

        Log.InfoSuccess(logger, request.VoucherId);
        return result;
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.PromotionsActivateVoucherStart, LogLevel.Information,
            "ActivateVoucher Start - VoucherId={VoucherId}")]
        public static partial void InfoStart(ILogger logger, Guid voucherId);

        [LoggerMessage((int)LogEventId.PromotionsActivateVoucherSuccess, LogLevel.Information,
            "ActivateVoucher Success - VoucherId={VoucherId}")]
        public static partial void InfoSuccess(ILogger logger, Guid voucherId);
    }
}
