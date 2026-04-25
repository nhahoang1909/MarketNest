using MediatR;

namespace MarketNest.Core.Common.Events;

/// <summary>
///     Base interface for integration events — cross-module communication.
///     Domain events stay inside a bounded context; integration events cross boundaries.
///
///     Phase 1: dispatched in-process via MediatR (same as domain events, but semantically distinct).
///     Phase 3: dispatched via RabbitMQ/MassTransit — handlers remain unchanged.
///
///     All integration events are immutable records with stable serialization contracts.
/// </summary>
public interface IIntegrationEvent : INotification
{
    /// <summary>Unique identifier for this event instance (idempotency key).</summary>
    Guid EventId { get; }

    /// <summary>UTC timestamp when the event was created.</summary>
    DateTimeOffset OccurredAtUtc { get; }

    /// <summary>
    ///     Fully qualified event type name for deserialization routing.
    ///     Defaults to the CLR type name. Override for versioned event names.
    /// </summary>
    string EventType => GetType().FullName!;
}

/// <summary>
///     Base record for integration events — provides sensible defaults for
///     <see cref="IIntegrationEvent.EventId"/> and <see cref="IIntegrationEvent.OccurredAtUtc"/>.
///     All concrete integration events should inherit from this record.
/// </summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

