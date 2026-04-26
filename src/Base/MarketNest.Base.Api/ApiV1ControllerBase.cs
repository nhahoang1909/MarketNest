using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MarketNest.Core.Common;

namespace MarketNest.Base.Api;

[ApiController]
public abstract class ApiV1ControllerBase(IMediator mediator) : ControllerBase
{
    protected IMediator Mediator => mediator;

    protected IActionResult MapError(Error error) => error.Type switch
    {
        ErrorType.NotFound     => NotFound(new { error.Code, error.Message }),
        ErrorType.Conflict     => Conflict(new { error.Code, error.Message }),
        ErrorType.Validation   => BadRequest(new { error.Code, error.Message }),
        ErrorType.Unauthorized => Unauthorized(new { error.Code, error.Message }),
        ErrorType.Forbidden    => StatusCode(StatusCodes.Status403Forbidden, new { error.Code, error.Message }),
        _                      => Problem(error.Message)
    };
}

