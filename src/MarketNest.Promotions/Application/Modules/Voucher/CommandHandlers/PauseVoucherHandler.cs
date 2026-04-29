using MarketNest.Base.Infrastructure;

namespace MarketNest.Promotions.Application;

public partial class PauseVoucherHandler(
    IVoucherRepository repository,
    IAppLogger<PauseVoucherHandler> logger) : ICommandHandler<PauseVoucherCommand, bool>
{
    public async Task<Result<bool, Error>> Handle(PauseVoucherCommand request, CancellationToken cancellationToken)
    {
        Log.InfoStart(logger, request.VoucherId);

        var voucher = await repository.GetByKeyAsync(request.VoucherId, cancellationToken);
        Result<bool, Error> result = voucher.Pause();
        if (!result.IsSuccess) return result;

        await repository.SaveChangesAsync(cancellationToken);

        Log.InfoSuccess(logger, request.VoucherId);
        return result;
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.PromotionsPauseVoucherStart, LogLevel.Information,
            "PauseVoucher Start - VoucherId={VoucherId}")]
        public static partial void InfoStart(ILogger logger, Guid voucherId);

        [LoggerMessage((int)LogEventId.PromotionsPauseVoucherSuccess, LogLevel.Information,
            "PauseVoucher Success - VoucherId={VoucherId}")]
        public static partial void InfoSuccess(ILogger logger, Guid voucherId);
    }
}
