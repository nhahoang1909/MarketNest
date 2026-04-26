using MediatR;

namespace MarketNest.Base.Common;

/// <summary>
///     Handler for domain events. Phase 1: in-process via MediatR.
///     Phase 3+: externalized to RabbitMQ via MassTransit.
/// </summary>
public interface IDomainEventHandler<TEvent> : INotificationHandler<TEvent>
    where TEvent : IDomainEvent;
