using MarketNest.Base.Api;
using MarketNest.Catalog.Application;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MarketNest.Catalog.Infrastructure;

/// <summary>
///     Admin endpoint for force-ending any seller's sale price.
///     Route: api/v1/admin/catalog/variants/{variantId}/sale
/// </summary>
[Route("admin/catalog/variants/{variantId:guid}/sale")]
public class VariantSaleAdminController(IMediator mediator) : ApiV1ControllerBase(mediator)
{
    /// <summary>Force-end a variant's sale price (admin override).</summary>
    [HttpDelete]
    public async Task<IActionResult> ForceRemoveSale(Guid variantId, CancellationToken ct)
    {
        var command = new RemoveSalePriceCommand(variantId, GetCurrentUserId(), IsAdmin: true);
        Result<Unit, Error> result = await Mediator.Send(command, ct);
        return result.IsSuccess ? NoContent() : MapError(result.Error);
    }

    private Guid GetCurrentUserId() =>
        Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out Guid id)
            ? id
            : Guid.Empty;
}

