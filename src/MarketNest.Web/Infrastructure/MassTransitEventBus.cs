// ──────────────────────────────────────────────────────────────────────────────
// Phase 3 ONLY — do NOT use in Phase 1.
// Uncomment and register when migrating from in-process events to RabbitMQ.
// ──────────────────────────────────────────────────────────────────────────────
//
// using MarketNest.Core.Common.Events;
// using MarketNest.Core.Logging;
// using MassTransit;
//
// namespace MarketNest.Web.Infrastructure;
//
// /// <summary>
// ///     Phase 3 implementation — publishes integration events to RabbitMQ via MassTransit.
// ///     Drop-in replacement for <see cref="InProcessEventBus"/>.
// ///
// ///     Registration in Program.cs:
// ///     <code>
// ///     builder.Services.AddSingleton&lt;IEventBus, MassTransitEventBus&gt;();
// ///     </code>
// ///
// ///     All existing <see cref="IIntegrationEventHandler{TEvent}"/> implementations are
// ///     auto-bridged to MassTransit consumers via <see cref="IntegrationEventConsumerAdapter{TEvent}"/>.
// /// </summary>
// internal sealed class MassTransitEventBus(IPublishEndpoint publishEndpoint, IAppLogger<MassTransitEventBus> logger) : IEventBus
// {
//     public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
//         where TEvent : class, IIntegrationEvent
//     {
//         var eventTypeName = @event.GetType().Name;
//
//         logger.Info(
//             "Publishing integration event {EventType} (Id: {EventId}) to RabbitMQ",
//             eventTypeName,
//             @event.EventId);
//
//         await publishEndpoint.Publish(@event, cancellationToken);
//
//         logger.Info(
//             "Successfully published integration event {EventType} (Id: {EventId}) to RabbitMQ",
//             eventTypeName,
//             @event.EventId);
//     }
// }

