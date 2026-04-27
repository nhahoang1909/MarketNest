using MediatR;

#pragma warning disable CA1711 // Keep name IDomainEventHandler for compatibility during migration

namespace MarketNest.Base.Domain;

/// <summary>
///     Handler for domain events. Phase 1: in-process via MediatR.
///     Phase 3+: externalized to RabbitMQ via MassTransit.
/// </summary>
public interface IDomainEventHandler<TEvent> : INotificationHandler<TEvent>
    where TEvent : IDomainEvent;
