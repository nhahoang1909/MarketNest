// ──────────────────────────────────────────────────────────────────────────────
// Phase 3 ONLY — do NOT use in Phase 1.
// This adapter bridges IIntegrationEventHandler<T> to MassTransit IConsumer<T>.
// Existing handlers don't need to change — this adapter wraps them automatically.
// ──────────────────────────────────────────────────────────────────────────────
//
// using MarketNest.Core.Common.Events;
// using MassTransit;
//
// namespace MarketNest.Web.Infrastructure;
//
// /// <summary>
// ///     Bridges <see cref="IIntegrationEventHandler{TEvent}"/> to MassTransit's
// ///     <see cref="IConsumer{TMessage}"/> so existing handlers work without modification.
// ///
// ///     Registration (in Phase 3 MassTransit setup):
// ///     <code>
// ///     cfg.AddConsumer&lt;IntegrationEventConsumerAdapter&lt;OrderPlacedIntegrationEvent&gt;&gt;();
// ///     </code>
// ///
// ///     Or auto-register all adapters via reflection over assemblies that contain
// ///     <see cref="IIntegrationEventHandler{TEvent}"/> implementations.
// /// </summary>
// /// <typeparam name="TEvent">The integration event type.</typeparam>
// internal sealed class IntegrationEventConsumerAdapter<TEvent>(
//     IEnumerable<IIntegrationEventHandler<TEvent>> handlers) : IConsumer<TEvent>
//     where TEvent : class, IIntegrationEvent
// {
//     public async Task Consume(ConsumeContext<TEvent> context)
//     {
//         foreach (var handler in handlers)
//         {
//             await handler.Handle(context.Message, context.CancellationToken);
//         }
//     }
// }


