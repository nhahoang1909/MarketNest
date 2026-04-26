using MediatR;
using MarketNest.Base.Api;

namespace MarketNest.Admin.Infrastructure;

// Thin wrapper kept in the Admin.Infrastructure namespace so existing controllers
// that reference ApiV1ControllerBase do not need changes. The implementation
// now lives in MarketNest.Base.Api.
public abstract class ApiV1ControllerBase(IMediator mediator) : MarketNest.Base.Api.ApiV1ControllerBase(mediator)
{
}
