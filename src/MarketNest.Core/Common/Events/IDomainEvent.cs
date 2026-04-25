using MediatR;

namespace MarketNest.Core.Common;

/// <summary>
///     Marker interface for domain events. Raised inside aggregate methods,
///     dispatched after SaveChanges via MediatR IPublisher.
/// </summary>
public interface IDomainEvent : INotification
{
    Guid EventId => Guid.NewGuid();
    DateTimeOffset OccurredAt => DateTimeOffset.UtcNow;
}
