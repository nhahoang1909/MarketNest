using MarketNest.Base.Api;
using MarketNest.Base.Common;
using MarketNest.Promotions.Application;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MarketNest.Promotions.Infrastructure;

[Route("promotions/vouchers")]
public class VoucherReadController(IMediator mediator) : ApiV1ControllerBase(mediator)
{
    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] GetVouchersPagedQuery query, CancellationToken ct)
        => Ok(await Mediator.Send(query, ct));

    [HttpGet("{id:guid}", Name = "GetVoucherById")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        VoucherDto? dto = await Mediator.Send(new GetVoucherByIdQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }
}
