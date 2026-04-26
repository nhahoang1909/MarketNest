using MediatR;

namespace MarketNest.Admin.Infrastructure;

public abstract class WriteApiV1ControllerBase(IMediator mediator)
    : ApiV1ControllerBase(mediator);
