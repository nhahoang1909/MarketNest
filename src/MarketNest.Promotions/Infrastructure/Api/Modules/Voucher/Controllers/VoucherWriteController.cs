using MarketNest.Base.Api;
using MarketNest.Promotions.Application;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MarketNest.Promotions.Infrastructure;

[Route("promotions/vouchers")]
public class VoucherWriteController(IMediator mediator) : ApiV1ControllerBase(mediator)
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateVoucherCommand command, CancellationToken ct)
    {
        Result<Guid, Error> result = await Mediator.Send(command, ct);
        return result.IsSuccess
            ? CreatedAtRoute("GetVoucherById", new { id = result.Value }, result.Value)
            : MapError(result.Error);
    }

    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        Result<bool, Error> result = await Mediator.Send(new ActivateVoucherCommand(id, GetCurrentUserId()), ct);
        return result.IsSuccess ? NoContent() : MapError(result.Error);
    }

    [HttpPatch("{id:guid}/pause")]
    public async Task<IActionResult> Pause(Guid id, CancellationToken ct)
    {
        Result<bool, Error> result = await Mediator.Send(new PauseVoucherCommand(id, GetCurrentUserId()), ct);
        return result.IsSuccess ? NoContent() : MapError(result.Error);
    }

    private Guid GetCurrentUserId() =>
        Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out Guid id)
            ? id
            : Guid.Empty;
}
