namespace MarketNest.Base.Common;

/// <summary>Variables for order.placed.buyer / order.placed.seller templates.</summary>
public record OrderPlacedVariables(
    string OrderNumber,
    string BuyerName,
    string SellerStoreName,
    string OrderTotal,
    string OrderUrl,
    string EstimatedDelivery);

/// <summary>Variables for order.shipped.buyer template.</summary>
public record OrderShippedVariables(
    string OrderNumber,
    string BuyerName,
    string TrackingNumber,
    string TrackingUrl,
    string OrderUrl);

/// <summary>Variables for order.confirmed.buyer template.</summary>
public record OrderConfirmedVariables(
    string OrderNumber,
    string BuyerName,
    string OrderUrl);

/// <summary>Variables for order.delivered.buyer template.</summary>
public record OrderDeliveredVariables(
    string OrderNumber,
    string BuyerName,
    string OrderUrl);

/// <summary>Variables for order.cancelled.buyer / order.cancelled.seller templates.</summary>
public record OrderCancelledVariables(
    string OrderNumber,
    string BuyerName,
    string SellerStoreName,
    string CancelReason,
    string OrderUrl);

/// <summary>Variables for dispute.opened.seller / dispute.opened.admin templates.</summary>
public record DisputeOpenedVariables(
    string OrderNumber,
    string BuyerName,
    string DisputeReason,
    string DisputeUrl,
    string ResponseDeadline);

/// <summary>Variables for dispute.resolved.buyer / dispute.resolved.seller templates.</summary>
public record DisputeResolvedVariables(
    string OrderNumber,
    string Resolution,
    string DisputeUrl);

/// <summary>Variables for payout.processed.seller template.</summary>
public record PayoutProcessedVariables(
    string SellerName,
    string GrossAmount,
    string CommissionDeducted,
    string NetAmount,
    string PayoutUrl);

/// <summary>Variables for review.received.seller template.</summary>
public record ReviewReceivedVariables(
    string ProductName,
    string ReviewerName,
    string Rating,
    string ReviewUrl);

/// <summary>Variables for inventory.low.seller template.</summary>
public record InventoryLowVariables(
    string ProductName,
    string VariantName,
    string CurrentStock,
    string ProductUrl);

/// <summary>Variables for security.password-reset template.</summary>
public record PasswordResetVariables(
    string UserName,
    string ResetUrl,
    string ExpiresIn);

/// <summary>Variables for security.new-login template.</summary>
public record NewLoginVariables(
    string UserName,
    string DeviceInfo,
    string IpAddress,
    string LoginTime);

