using MediatR;

namespace MarketNest.Admin.Infrastructure;

public abstract class ReadApiV1ControllerBase(IMediator mediator)
    : ApiV1ControllerBase(mediator);
