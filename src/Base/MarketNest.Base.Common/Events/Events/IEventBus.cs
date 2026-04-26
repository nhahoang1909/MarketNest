namespace MarketNest.Base.Common;

/// <summary>
///     Abstraction for publishing integration events across bounded contexts.
///     Phase 1: <c>InProcessEventBus</c> wraps MediatR IPublisher (in-process dispatch).
///     Phase 3: <c>MassTransitEventBus</c> wraps MassTransit IPublishEndpoint (RabbitMQ).
///     Module code depends ONLY on this interface — the transport is a DI swap.
/// </summary>
public interface IEventBus
{
    /// <summary>
    ///     Publishes an integration event to all registered handlers.
    /// </summary>
    /// <param name="event">The integration event to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent;
}
