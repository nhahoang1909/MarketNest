using MediatR;
using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Phase 1 implementation — dispatches integration events in-process via MediatR.
///     Phase 3 migration: replace this registration with <c>MassTransitEventBus</c>.
/// </summary>
internal sealed partial class InProcessEventBus(IPublisher publisher, IAppLogger<InProcessEventBus> logger) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        var eventTypeName = integrationEvent.GetType().Name;
        Log.InfoPublishStart(logger, eventTypeName, integrationEvent.EventId);

        try
        {
            await publisher.Publish(integrationEvent, cancellationToken);
            Log.InfoPublishSuccess(logger, eventTypeName, integrationEvent.EventId);
        }
        catch (Exception ex)
        {
            Log.ErrorPublishFailed(logger, eventTypeName, integrationEvent.EventId, ex);
            throw;
        }
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.EventBusPublishStart, LogLevel.Information,
            "Publishing integration event {EventType} (Id: {EventId})")]
        public static partial void InfoPublishStart(ILogger logger, string eventType, Guid eventId);

        [LoggerMessage((int)LogEventId.EventBusPublishSuccess, LogLevel.Information,
            "Successfully dispatched integration event {EventType} (Id: {EventId})")]
        public static partial void InfoPublishSuccess(ILogger logger, string eventType, Guid eventId);

        [LoggerMessage((int)LogEventId.EventBusPublishError, LogLevel.Error,
            "Failed to dispatch integration event {EventType} (Id: {EventId})")]
        public static partial void ErrorPublishFailed(ILogger logger, string eventType, Guid eventId, Exception ex);
    }
}
