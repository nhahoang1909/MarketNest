using MediatR;

namespace MarketNest.Core.Common.Events;

/// <summary>
/// Handler for domain events. Phase 1: in-process via MediatR.
/// Phase 3+: externalized to RabbitMQ via MassTransit.
/// </summary>
#pragma warning disable CA1711
public interface IDomainEventHandler<TEvent> : INotificationHandler<TEvent>
#pragma warning restore CA1711
    where TEvent : IDomainEvent;
