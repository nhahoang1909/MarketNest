using MarketNest.Base.Infrastructure;
using MarketNest.Base.Utility;

namespace MarketNest.Promotions.Application;

public partial class VoucherExpiryJob(
    IVoucherRepository repository,
    IUnitOfWork uow,
    IAppLogger<VoucherExpiryJob> logger) : IBackgroundJob
{
    private const string JobKeyValue = "promotions.voucher.expiry";
    private const string ModuleName = "Promotions";

    public JobDescriptor Descriptor { get; } = new(
        JobKeyValue,
        "Expire and deplete vouchers past their deadline or usage limit",
        ModuleName,
        JobType.Timer,
        "01:00:00",
        true,
        false,
        0,
        "Runs hourly. Sets Status=Expired for past ExpiryDate vouchers; Status=Depleted for fully-used vouchers.");

    public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken = default)
    {
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;

        IReadOnlyList<Domain.Voucher> toExpire =
            await repository.GetActiveExpiredAsync(utcNow, cancellationToken);

        IReadOnlyList<Domain.Voucher> toDepleted =
            await repository.GetActiveDepletedAsync(cancellationToken);

        if (toExpire.Count == 0 && toDepleted.Count == 0)
        {
            Log.InfoCompleted(logger, context.ExecutionId, 0, 0);
            return;
        }

        try
        {
            await uow.BeginTransactionAsync(ct: cancellationToken);

            foreach (Domain.Voucher voucher in toExpire)
            {
                voucher.MarkExpired();
                Log.InfoExpired(logger, voucher.Id);
            }

            foreach (Domain.Voucher voucher in toDepleted)
            {
                voucher.MarkDepleted();
                Log.InfoDepleted(logger, voucher.Id);
            }

            await uow.CommitAsync(cancellationToken);
            await uow.CommitTransactionAsync(cancellationToken);
            await uow.DispatchPostCommitEventsAsync(cancellationToken);

            Log.InfoCompleted(logger, context.ExecutionId, toExpire.Count, toDepleted.Count);
        }
        catch (Exception ex)
        {
            await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
            Log.ErrorFailed(logger, context.ExecutionId, ex);
            throw;
        }
        finally
        {
            await uow.DisposeAsync();
        }
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.PromotionsExpiryJobExpired, LogLevel.Information,
            "VoucherExpiryJob: Voucher expired - VoucherId={VoucherId}")]
        public static partial void InfoExpired(ILogger logger, Guid voucherId);

        [LoggerMessage((int)LogEventId.PromotionsExpiryJobDepleted, LogLevel.Information,
            "VoucherExpiryJob: Voucher depleted - VoucherId={VoucherId}")]
        public static partial void InfoDepleted(ILogger logger, Guid voucherId);

        [LoggerMessage((int)LogEventId.PromotionsExpiryJobCompleted, LogLevel.Information,
            "VoucherExpiryJob Completed - ExecutionId={ExecutionId}, ExpiredCount={ExpiredCount}, DepletedCount={DepletedCount}")]
        public static partial void InfoCompleted(ILogger logger, Guid executionId, int expiredCount, int depletedCount);

        [LoggerMessage((int)LogEventId.PromotionsExpiryJobError, LogLevel.Error,
            "VoucherExpiryJob Failed - ExecutionId={ExecutionId}")]
        public static partial void ErrorFailed(ILogger logger, Guid executionId, Exception ex);
    }
}
