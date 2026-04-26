using MediatR;
using Microsoft.AspNetCore.Mvc;
using MarketNest.Admin.Application;

namespace MarketNest.Admin.Infrastructure;

[Route("api/v1/admin/tests")]
public class TestReadController(IMediator mediator) : ReadApiV1ControllerBase(mediator)
{
    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] GetTestsPagedQuery query, CancellationToken ct)
        => Ok(await Mediator.Send(query, ct));

    [HttpGet("{id:guid}", Name = "GetTestById")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await Mediator.Send(new GetTestByIdQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }
}
