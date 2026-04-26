using MediatR;

namespace MarketNest.Core.Common.Events;

/// <summary>
///     Handler for integration events — cross-module event processing.
///
///     Phase 1: resolved and invoked in-process via MediatR INotificationHandler.
///     Phase 3: bridged to MassTransit IConsumer&lt;T&gt; — handler code stays identical.
///
///     Each handler should be idempotent (use <see cref="IIntegrationEvent.EventId"/>
///     for dedup) since message brokers may deliver at-least-once.
/// </summary>
/// <typeparam name="TEvent">The integration event type to handle.</typeparam>
public interface IIntegrationEventHandler<in TEvent> : INotificationHandler<TEvent>
    where TEvent : IIntegrationEvent;

