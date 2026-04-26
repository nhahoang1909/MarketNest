using MediatR;
using Microsoft.AspNetCore.Mvc;
using MarketNest.Admin.Application;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Infrastructure;

[Route("api/v1/admin/tests")]
public class TestWriteController(IMediator mediator) : WriteApiV1ControllerBase(mediator)
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTestRequest req, CancellationToken ct)
    {
        var cmd = new CreateTestCommand(
            req.Name,
            new TestValueObject { Code = req.ValueCode, Amount = req.ValueAmount },
            req.SubTitles);
        var result = await Mediator.Send(cmd, ct);
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
        var result = await Mediator.Send(cmd, ct);
        if (result.IsFailure) return MapError(result.Error);
        return NoContent();
    }

    public record CreateTestRequest(
        string Name, string ValueCode, decimal ValueAmount,
        IEnumerable<string>? SubTitles = null);

    public record UpdateTestRequest(
        string Name, string ValueCode, decimal ValueAmount,
        IEnumerable<string>? SubTitles = null);
}
