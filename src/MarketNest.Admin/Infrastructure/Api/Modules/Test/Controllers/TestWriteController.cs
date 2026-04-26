using MarketNest.Admin.Application;
using MarketNest.Admin.Domain;
using MarketNest.Base.Api;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MarketNest.Admin.Infrastructure;

[Route("admin/tests")]
public class TestWriteController(IMediator mediator) : ApiV1ControllerBase(mediator)
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTestRequest req, CancellationToken ct)
    {
        var cmd = new CreateTestCommand(
            req.Name,
            new TestValueObject { Code = req.ValueCode, Amount = req.ValueAmount },
            req.SubTitles);
        Result<Guid, Error> result = await Mediator.Send(cmd, ct);
        if (result.IsFailure) return MapError(result.Error);
        return CreatedAtRoute("GetTestById", new { id = result.Value }, new { id = result.Value });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateTestRequest req, CancellationToken ct)
    {
        var cmd = new UpdateTestCommand(
            id,
            req.Name,
            new TestValueObject { Code = req.ValueCode, Amount = req.ValueAmount },
            req.SubTitles);
        Result<Unit, Error> result = await Mediator.Send(cmd, ct);
        if (result.IsFailure) return MapError(result.Error);
        return NoContent();
    }

    public record CreateTestRequest(
        string Name,
        string ValueCode,
        decimal ValueAmount,
        IEnumerable<string>? SubTitles = null);

    // Request DTOs moved to: Api/Modules/Test/TestRequests.cs
}
