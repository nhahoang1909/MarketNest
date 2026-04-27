using MarketNest.Base.Common;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MarketNest.Base.Api;

[ApiController]
[Route("api/v1")]

// Base route now provides the api/v1 prefix. Child controllers should provide the module/resource
// segment (for example: [Route("admin/tests")]). This keeps routes consistent with project
// convention: api/v1/{module}/{resource}.
public abstract class ApiV1ControllerBase(IMediator mediator) : ControllerBase
{
    protected IMediator Mediator => mediator;

    protected IActionResult MapError(Error error) => error.Type switch
    {
        ErrorType.NotFound => NotFound(new { error.Code, error.Message }),
        ErrorType.Conflict => Conflict(new { error.Code, error.Message }),
        ErrorType.Validation => BadRequest(new { error.Code, error.Message }),
        ErrorType.Unauthorized => Unauthorized(new { error.Code, error.Message }),
        ErrorType.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { error.Code, error.Message }),
        _ => Problem(error.Message)
    };
}
