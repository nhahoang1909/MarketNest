using Microsoft.AspNetCore.Mvc;
using MediatR;
using MarketNest.Core.Common;
using MarketNest.Admin.Application;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Infrastructure;

[ApiController]
[Route("api/admin/tests")]
public class TestsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TestsController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTestRequest req, CancellationToken cancellationToken)
    {
        var cmd = new CreateTestCommand(req.Name, new TestValueObject { Code = req.ValueCode, Amount = req.ValueAmount }, req.SubTitles);
        var result = await _mediator.Send(cmd, cancellationToken);
        if (result.IsFailure) return Problem(result.Error.Message);
        return CreatedAtAction(nameof(GetById), new { id = result.Value }, null);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTestRequest req, CancellationToken cancellationToken)
    {
        var cmd = new UpdateTestCommand(id, req.Name, new TestValueObject { Code = req.ValueCode, Amount = req.ValueAmount }, req.SubTitles);
        var result = await _mediator.Send(cmd, cancellationToken);
        if (result.IsFailure) return Problem(result.Error.Message);
        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] GetTestsPagedQuery query, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var dto = await _mediator.Send(new GetTestByIdQuery(id), cancellationToken);
        if (dto is null) return NotFound();
        return Ok(dto);
    }

    public record CreateTestRequest(string Name, string ValueCode, decimal ValueAmount, IEnumerable<string>? SubTitles = null);
    public record UpdateTestRequest(string Name, string ValueCode, decimal ValueAmount, IEnumerable<string>? SubTitles = null);
}

