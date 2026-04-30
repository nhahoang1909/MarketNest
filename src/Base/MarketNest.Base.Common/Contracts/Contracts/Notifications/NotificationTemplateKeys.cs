namespace MarketNest.Base.Common;

/// <summary>
///     Stable template key constants referenced from code and DB seeders.
///     Keys are immutable — changing them requires a data migration.
/// </summary>
public static class NotificationTemplateKeys
{
    // ── Orders ────────────────────────────────────────────────────────────
    public const string OrderPlacedBuyer = "order.placed.buyer";
    public const string OrderPlacedSeller = "order.placed.seller";
    public const string OrderConfirmedBuyer = "order.confirmed.buyer";
    public const string OrderShippedBuyer = "order.shipped.buyer";
    public const string OrderDeliveredBuyer = "order.delivered.buyer";
    public const string OrderCancelledBuyer = "order.cancelled.buyer";
    public const string OrderCancelledSeller = "order.cancelled.seller";

    // ── Disputes ──────────────────────────────────────────────────────────
    public const string DisputeOpenedSeller = "dispute.opened.seller";
    public const string DisputeOpenedAdmin = "dispute.opened.admin";
    public const string DisputeRespondedBuyer = "dispute.responded.buyer";
    public const string DisputeResolvedBuyer = "dispute.resolved.buyer";
    public const string DisputeResolvedSeller = "dispute.resolved.seller";

    // ── Payments ──────────────────────────────────────────────────────────
    public const string PayoutProcessedSeller = "payout.processed.seller";

    // ── Catalog ───────────────────────────────────────────────────────────
    public const string ReviewReceivedSeller = "review.received.seller";
    public const string InventoryLowSeller = "inventory.low.seller";

    // ── Security (always sent, no toggle) ─────────────────────────────────
    public const string PasswordResetRequest = "security.password-reset";
    public const string NewLoginUnknownDevice = "security.new-login";
}

