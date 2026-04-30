using MarketNest.Base.Infrastructure;
using MarketNest.Catalog.Domain;

namespace MarketNest.Catalog.Application;

public partial class SetSalePriceHandler(
    IVariantRepository repository,
    IAppLogger<SetSalePriceHandler> logger) : ICommandHandler<SetSalePriceCommand, Unit>
{
    public async Task<Result<Unit, Error>> Handle(SetSalePriceCommand request, CancellationToken cancellationToken)
    {
        Log.InfoStart(logger, request.VariantId, request.SalePrice);

        ProductVariant? variant = await repository.GetByProductAsync(
            request.ProductId, request.VariantId, cancellationToken);

        if (variant is null)
            return Result<Unit, Error>.Failure(
                Error.NotFound(nameof(ProductVariant), request.VariantId.ToString()));

        Money salePrice = new(request.SalePrice, DomainConstants.Currencies.Default);
        Result<Unit, Error> result = variant.SetSalePrice(salePrice, request.SaleStart, request.SaleEnd);

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
        [LoggerMessage((int)LogEventId.CatalogSetSalePriceStart, LogLevel.Information,
            "SetSalePrice Start - VariantId={VariantId}, SalePrice={SalePrice}")]
        public static partial void InfoStart(ILogger logger, Guid variantId, decimal salePrice);

        [LoggerMessage((int)LogEventId.CatalogSetSalePriceSuccess, LogLevel.Information,
            "SetSalePrice Success - VariantId={VariantId}")]
        public static partial void InfoSuccess(ILogger logger, Guid variantId);

        [LoggerMessage((int)LogEventId.CatalogSetSalePriceFailed, LogLevel.Warning,
            "SetSalePrice Failed - VariantId={VariantId}, ErrorCode={ErrorCode}")]
        public static partial void WarnFailed(ILogger logger, Guid variantId, string errorCode);
    }
}
