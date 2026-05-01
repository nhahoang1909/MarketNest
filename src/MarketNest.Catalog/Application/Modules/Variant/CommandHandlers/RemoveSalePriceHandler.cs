using MarketNest.Base.Infrastructure;
using MarketNest.Catalog.Domain;

namespace MarketNest.Catalog.Application;

public partial class RemoveSalePriceCommandHandler(
    IVariantRepository repository,
    IAppLogger<RemoveSalePriceCommandHandler> logger) : ICommandHandler<RemoveSalePriceCommand, Unit>
{
    public async Task<Result<Unit, Error>> Handle(RemoveSalePriceCommand request, CancellationToken cancellationToken)
    {
        Log.InfoStart(logger, request.VariantId);

        ProductVariant? variant = await repository.FindByKeyAsync(request.VariantId, cancellationToken);

        if (variant is null)
            return Result<Unit, Error>.Failure(
                Error.NotFound(nameof(ProductVariant), request.VariantId.ToString()));

        Result<Unit, Error> result = variant.RemoveSalePrice();

        if (result.IsFailure)
        {
            Log.WarnFailed(logger, request.VariantId, result.Error.Code);
            return result;
        }

        repository.Update(variant);

        Log.InfoSuccess(logger, request.VariantId);
        return result;
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.CatalogRemoveSalePriceStart, LogLevel.Information,
            "RemoveSalePrice Start - VariantId={VariantId}")]
        public static partial void InfoStart(ILogger logger, Guid variantId);

        [LoggerMessage((int)LogEventId.CatalogRemoveSalePriceSuccess, LogLevel.Information,
            "RemoveSalePrice Success - VariantId={VariantId}")]
        public static partial void InfoSuccess(ILogger logger, Guid variantId);

        [LoggerMessage((int)LogEventId.CatalogRemoveSalePriceFailed, LogLevel.Warning,
            "RemoveSalePrice Failed - VariantId={VariantId}, ErrorCode={ErrorCode}")]
        public static partial void WarnFailed(ILogger logger, Guid variantId, string errorCode);
    }
}
