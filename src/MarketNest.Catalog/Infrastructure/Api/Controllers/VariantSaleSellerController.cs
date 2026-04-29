using MarketNest.Base.Api;
using MarketNest.Catalog.Application;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MarketNest.Catalog.Infrastructure;

/// <summary>
///     Seller endpoints for managing timed sale prices on product variants.
///     Route: api/v1/seller/products/{productId}/variants/{variantId}/sale
/// </summary>
[Route("seller/products/{productId:guid}/variants/{variantId:guid}/sale")]
public class VariantSaleSellerController(IMediator mediator) : ApiV1ControllerBase(mediator)
{
    /// <summary>Set or overwrite a timed sale price on a variant.</summary>
    [HttpPatch]
    public async Task<IActionResult> SetSale(
        Guid productId,
        Guid variantId,
        [FromBody] SetSaleRequest request,
        CancellationToken ct)
    {
        var command = new SetSalePriceCommand(
            productId,
            variantId,
            GetCurrentUserId(),
            request.SalePrice,
            request.SaleStart,
            request.SaleEnd);

        Result<Unit, Error> result = await Mediator.Send(command, ct);
        return result.IsSuccess ? NoContent() : MapError(result.Error);
    }

    /// <summary>Remove the active sale price from a variant immediately.</summary>
    [HttpDelete]
    public async Task<IActionResult> RemoveSale(
        Guid variantId,
        CancellationToken ct)
    {
        var command = new RemoveSalePriceCommand(variantId, GetCurrentUserId());
        Result<Unit, Error> result = await Mediator.Send(command, ct);
        return result.IsSuccess ? NoContent() : MapError(result.Error);
    }

    private Guid GetCurrentUserId() =>
        Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out Guid id)
            ? id
            : Guid.Empty;

    public record SetSaleRequest(
        decimal SalePrice,
        DateTimeOffset SaleStart,
        DateTimeOffset SaleEnd);
}

