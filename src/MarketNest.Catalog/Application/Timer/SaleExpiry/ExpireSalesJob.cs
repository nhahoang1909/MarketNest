using MarketNest.Base.Infrastructure;
using MarketNest.Base.Utility;
using MarketNest.Catalog.Domain;

namespace MarketNest.Catalog.Application;

/// <summary>
///     Background job that cleans up expired sale prices on variants.
///     Runs every 5 minutes. Calls <c>RemoveSalePrice()</c> to raise the
///     <see cref="VariantSalePriceRemovedEvent"/> domain event for downstream notifications.
/// </summary>
public partial class ExpireSalesJob(
    IVariantRepository repository,
    IUnitOfWork uow,
    IAppLogger<ExpireSalesJob> logger) : IBackgroundJob
{
    public JobDescriptor Descriptor { get; } = new(
        CatalogConstants.Sale.ExpiryJobKey,
        "Expire timed sale prices on product variants",
        "Catalog",
        JobType.Timer,
        CatalogConstants.Sale.ExpiryJobSchedule,
        true,
        false,
        0,
        "Runs every 5 minutes. Clears SalePrice/SaleStart/SaleEnd on variants whose sale window has ended.");

    public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken = default)
    {
        Log.InfoStart(logger, context.ExecutionId);

        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        IReadOnlyList<ProductVariant> expired =
            await repository.GetExpiredSalesAsync(utcNow, cancellationToken);

        if (expired.Count == 0)
        {
            Log.InfoCompleted(logger, context.ExecutionId, 0);
            return;
        }

        try
        {
            await uow.BeginTransactionAsync(ct: cancellationToken);

            foreach (ProductVariant variant in expired)
            {
                variant.RemoveSalePrice();
                Log.InfoExpired(logger, variant.Id);
            }

            await uow.CommitAsync(cancellationToken);
            await uow.CommitTransactionAsync(cancellationToken);
            await uow.DispatchPostCommitEventsAsync(cancellationToken);

            Log.InfoCompleted(logger, context.ExecutionId, expired.Count);
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
        [LoggerMessage((int)LogEventId.CatalogSaleExpiryJobStart, LogLevel.Information,
            "ExpireSalesJob Start - ExecutionId={ExecutionId}")]
        public static partial void InfoStart(ILogger logger, Guid executionId);

        [LoggerMessage((int)LogEventId.CatalogSaleExpiryJobExpired, LogLevel.Information,
            "ExpireSalesJob: Sale expired - VariantId={VariantId}")]
        public static partial void InfoExpired(ILogger logger, Guid variantId);

        [LoggerMessage((int)LogEventId.CatalogSaleExpiryJobCompleted, LogLevel.Information,
            "ExpireSalesJob Completed - ExecutionId={ExecutionId}, ExpiredCount={ExpiredCount}")]
        public static partial void InfoCompleted(ILogger logger, Guid executionId, int expiredCount);

        [LoggerMessage((int)LogEventId.CatalogSaleExpiryJobError, LogLevel.Error,
            "ExpireSalesJob Failed - ExecutionId={ExecutionId}")]
        public static partial void ErrorFailed(ILogger logger, Guid executionId, Exception ex);
    }
}



