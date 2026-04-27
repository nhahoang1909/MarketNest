// ──────────────────────────────────────────────────────────────────────────────
// Phase 3 ONLY — do NOT use in Phase 1.
// Uncomment and register when migrating from in-process events to RabbitMQ.
// ──────────────────────────────────────────────────────────────────────────────
//
// using MarketNest.Base.Common;
// using MarketNest.Base.Infrastructure;
// using MassTransit;
//
// namespace MarketNest.Web.Infrastructure;
//
// internal sealed partial class MassTransitEventBus(
//     IPublishEndpoint publishEndpoint,
//     IAppLogger<MassTransitEventBus> logger) : IEventBus
// {
//     public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
//         where TEvent : class, IIntegrationEvent
//     {
//         var eventTypeName = @event.GetType().Name;
//         Log.InfoPublishStart(logger, eventTypeName, @event.EventId);
//
//         await publishEndpoint.Publish(@event, cancellationToken);
//
//         Log.InfoPublishSuccess(logger, eventTypeName, @event.EventId);
//     }
//
//     private static partial class Log
//     {
//         [LoggerMessage((int)LogEventId.MassTransitPublishStart, LogLevel.Information,
//             "Publishing integration event {EventType} (Id: {EventId}) to RabbitMQ")]
//         public static partial void InfoPublishStart(ILogger logger, string eventType, Guid eventId);
//
//         [LoggerMessage((int)LogEventId.MassTransitPublishSuccess, LogLevel.Information,
//             "Successfully published integration event {EventType} (Id: {EventId}) to RabbitMQ")]
//         public static partial void InfoPublishSuccess(ILogger logger, string eventType, Guid eventId);
//     }
// }
