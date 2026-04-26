namespace MarketNest.Base.Common;

/// <summary>
/// Order status string constants used for display and matching.
/// These map to the <c>OrderStatus</c> enum values in the Orders module.
/// </summary>
public static class OrderStatusNames
{
    public const string PendingPayment = "pending payment";
    public const string Pending = "pending";
    public const string Confirmed = "confirmed";
    public const string Paid = "paid";
    public const string Processing = "processing";
    public const string Shipped = "shipped";
    public const string InTransit = "in transit";
    public const string Delivered = "delivered";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    public const string Refunded = "refunded";
    public const string Disputed = "disputed";
    public const string ReturnRequested = "return requested";
    public const string Unknown = "Unknown";
}

/// <summary>
/// General entity status string constants used across status badges.
/// </summary>
public static class EntityStatusNames
{
    public const string Active = "active";
    public const string Completed = "completed";
    public const string Approved = "approved";
    public const string Delivered = "delivered";
    public const string Pending = "pending";
    public const string Processing = "processing";
    public const string Cancelled = "cancelled";
    public const string Rejected = "rejected";
    public const string Failed = "failed";
    public const string Shipped = "shipped";
    public const string InTransit = "in transit";
    public const string Draft = "draft";
    public const string Refunded = "refunded";
    public const string Disputed = "disputed";
    public const string Suspended = "Suspended";
    public const string Unknown = "Unknown";

    // Display labels for role badges
    public const string Buyer = "Buyer";
    public const string Seller = "Seller";
    public const string Admin = "Admin";
}

