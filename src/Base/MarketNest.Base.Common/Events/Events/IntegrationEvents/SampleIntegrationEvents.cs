namespace MarketNest.Base.Common;

// ──────────────────────────────────────────────────────────────────────────────
// Example integration events — cross-module contracts.
//
// These live in MarketNest.Core because they are SHARED between bounded contexts.
// The publishing module creates the event; consuming modules handle it.
//
// Naming convention: <Noun><PastTenseVerb>IntegrationEvent
//   e.g., OrderPlacedIntegrationEvent, PaymentCompletedIntegrationEvent
//
// Rules:
//   1. Records only — immutable, serialization-friendly.
//   2. No entity references — only IDs and primitive data (stable contract).
//   3. Inherit from IntegrationEvent base record.
//   4. Keep payloads minimal — consumers can query their own data if needed.
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
///     Published by Orders module when a new order is placed.
///     Consumed by: Notifications (send confirmation), Payments (initiate charge),
///     Catalog (reserve inventory).
/// </summary>
public sealed record OrderPlacedIntegrationEvent : IntegrationEvent
{
    public required Guid OrderId { get; init; }
    public required Guid BuyerId { get; init; }
    public required Guid StorefrontId { get; init; }
    public required decimal TotalAmount { get; init; }
    public required string CurrencyCode { get; init; }
}

/// <summary>
///     Published by Payments module when payment is confirmed.
///     Consumed by: Orders (advance order state), Notifications (send receipt).
/// </summary>
public sealed record PaymentCompletedIntegrationEvent : IntegrationEvent
{
    public required Guid PaymentId { get; init; }
    public required Guid OrderId { get; init; }
    public required decimal Amount { get; init; }
    public required string CurrencyCode { get; init; }
}

/// <summary>
///     Published by Orders module when an order is shipped.
///     Consumed by: Notifications (send tracking info), Reviews (unlock review eligibility).
/// </summary>
public sealed record OrderShippedIntegrationEvent : IntegrationEvent
{
    public required Guid OrderId { get; init; }
    public required Guid BuyerId { get; init; }
    public required string TrackingNumber { get; init; }
    public required string Carrier { get; init; }
}

/// <summary>
///     Published by Identity module when a new user registers.
///     Consumed by: Notifications (send welcome email).
/// </summary>
public sealed record UserRegisteredIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
}

/// <summary>
///     Published by Catalog module when product stock reaches zero.
///     Consumed by: Notifications (alert seller), Cart (invalidate items).
/// </summary>
public sealed record ProductOutOfStockIntegrationEvent : IntegrationEvent
{
    public required Guid ProductId { get; init; }
    public required Guid StorefrontId { get; init; }
    public required string ProductName { get; init; }
}
