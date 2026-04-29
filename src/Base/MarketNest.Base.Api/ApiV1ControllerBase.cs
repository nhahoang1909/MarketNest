using MarketNest.Base.Common;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MarketNest.Base.Api;

/// <summary>
///     Internal shared base for all module API controllers.
///     Provides MediatR access and a unified <see cref="MapError" /> helper.
///     Prefer inheriting <see cref="ReadApiV1ControllerBase" /> or
///     <see cref="WriteApiV1ControllerBase" /> directly.
/// </summary>
[ApiController]
[Route("api/v1")]
// Base route provides the api/v1 prefix. Child controllers must add the module/resource
// segment (e.g. [Route("admin/tests")]). Convention: api/v1/{module}/{resource}.
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

/// <summary>
///     Base for read-only (GET) API controllers. No transaction wrapping.
///     Route prefix: <c>api/v1/{module}/{resource}</c>.
/// </summary>
public abstract class ReadApiV1ControllerBase(IMediator mediator) : ApiV1ControllerBase(mediator)
{
}

/// <summary>
///     Base for write (POST / PUT / DELETE / PATCH) API controllers.
///     Applies <c>[Transaction]</c> at the class level so that every write action is
///     automatically wrapped in a DB transaction by <c>TransactionActionFilter</c>
///     registered in <c>MarketNest.Web</c>. Use <c>[NoTransaction]</c> on individual
///     actions to opt out.
/// </summary>
[Transaction]
public abstract class WriteApiV1ControllerBase(IMediator mediator) : ApiV1ControllerBase(mediator)
{
}

