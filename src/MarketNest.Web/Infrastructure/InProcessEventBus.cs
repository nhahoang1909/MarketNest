using MediatR;
using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Phase 1 implementation — dispatches integration events in-process via MediatR.
///     Phase 3 migration: replace this registration with <c>MassTransitEventBus</c>.
///     All <see cref="IIntegrationEventHandler{TEvent}" /> implementations remain unchanged
///     because they depend on the abstraction, not the transport.
/// </summary>
internal sealed class InProcessEventBus(IPublisher publisher, IAppLogger<InProcessEventBus> logger) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        var eventTypeName = @event.GetType().Name;

        logger.Info(
            "Publishing integration event {EventType} (Id: {EventId})",
            eventTypeName,
            @event.EventId);

        try
        {
            await publisher.Publish(@event, cancellationToken);

            logger.Info(
                "Successfully dispatched integration event {EventType} (Id: {EventId})",
                eventTypeName,
                @event.EventId);
        }
        catch (Exception ex)
        {
            logger.Error(
                ex,
                "Failed to dispatch integration event {EventType} (Id: {EventId})",
                eventTypeName,
                @event.EventId);
            throw;
        }
    }
}
