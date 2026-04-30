using MarketNest.Base.Common;
using MarketNest.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Notifications.Infrastructure;

/// <summary>
///     Seeds default notification templates on startup. Only inserts missing keys —
///     never overwrites admin-customized templates.
/// </summary>
public class NotificationTemplateSeeder(NotificationsDbContext db) : IDataSeeder
{
    public int Order => 350;
    public bool RunInProduction => true;
    public string Version => "2026.04.30";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var templates = GetDefaultTemplates();

        foreach (var template in templates)
        {
            var exists = await db.NotificationTemplates
                .AnyAsync(t => t.TemplateKey == template.TemplateKey, ct);
            if (!exists)
                db.NotificationTemplates.Add(template);
        }

        await db.SaveChangesAsync(ct);
    }

    private static NotificationTemplate[] GetDefaultTemplates() =>
    [
        new(
            templateKey: NotificationTemplateKeys.OrderPlacedBuyer,
            displayName: "Order Placed — Buyer",
            channel: NotificationChannel.Both,
            subjectTemplate: "Your order #{{OrderNumber}} has been placed!",
            bodyTemplate: """
                <p>Hi {{BuyerName}},</p>
                <p>Your order <strong>#{{OrderNumber}}</strong> from <strong>{{SellerStoreName}}</strong>
                has been received. Total: <strong>{{OrderTotal}}</strong>.</p>
                <p>Estimated delivery: {{EstimatedDelivery}}</p>
                <p><a href="{{OrderUrl}}">View your order &rarr;</a></p>
                """,
            availableVariables: ["OrderNumber", "BuyerName", "SellerStoreName", "OrderTotal", "OrderUrl", "EstimatedDelivery"]),

        new(
            templateKey: NotificationTemplateKeys.OrderPlacedSeller,
            displayName: "Order Placed — Seller",
            channel: NotificationChannel.Both,
            subjectTemplate: "New order #{{OrderNumber}} received!",
            bodyTemplate: """
                <p>You have a new order <strong>#{{OrderNumber}}</strong> from <strong>{{BuyerName}}</strong>.</p>
                <p>Total: <strong>{{OrderTotal}}</strong>.</p>
                <p><a href="{{OrderUrl}}">View order details &rarr;</a></p>
                """,
            availableVariables: ["OrderNumber", "BuyerName", "SellerStoreName", "OrderTotal", "OrderUrl"]),

        new(
            templateKey: NotificationTemplateKeys.OrderConfirmedBuyer,
            displayName: "Order Confirmed — Buyer",
            channel: NotificationChannel.Both,
            subjectTemplate: "Your order #{{OrderNumber}} has been confirmed",
            bodyTemplate: """
                <p>Hi {{BuyerName}},</p>
                <p>Your order <strong>#{{OrderNumber}}</strong> has been confirmed by the seller.</p>
                <p><a href="{{OrderUrl}}">Track your order &rarr;</a></p>
                """,
            availableVariables: ["OrderNumber", "BuyerName", "OrderUrl"]),

        new(
            templateKey: NotificationTemplateKeys.OrderShippedBuyer,
            displayName: "Order Shipped — Buyer",
            channel: NotificationChannel.Both,
            subjectTemplate: "Your order #{{OrderNumber}} has been shipped!",
            bodyTemplate: """
                <p>Hi {{BuyerName}},</p>
                <p>Your order <strong>#{{OrderNumber}}</strong> has been shipped.</p>
                <p>Tracking number: <strong>{{TrackingNumber}}</strong></p>
                <p><a href="{{TrackingUrl}}">Track shipment &rarr;</a></p>
                """,
            availableVariables: ["OrderNumber", "BuyerName", "TrackingNumber", "TrackingUrl", "OrderUrl"]),

        new(
            templateKey: NotificationTemplateKeys.OrderDeliveredBuyer,
            displayName: "Order Delivered — Buyer",
            channel: NotificationChannel.Both,
            subjectTemplate: "Your order #{{OrderNumber}} has been delivered",
            bodyTemplate: """
                <p>Hi {{BuyerName}},</p>
                <p>Your order <strong>#{{OrderNumber}}</strong> has been delivered.</p>
                <p><a href="{{OrderUrl}}">Leave a review &rarr;</a></p>
                """,
            availableVariables: ["OrderNumber", "BuyerName", "OrderUrl"]),

        new(
            templateKey: NotificationTemplateKeys.OrderCancelledBuyer,
            displayName: "Order Cancelled — Buyer",
            channel: NotificationChannel.Both,
            subjectTemplate: "Your order #{{OrderNumber}} has been cancelled",
            bodyTemplate: """
                <p>Hi {{BuyerName}},</p>
                <p>Your order <strong>#{{OrderNumber}}</strong> from <strong>{{SellerStoreName}}</strong> has been cancelled.</p>
                <p>Reason: {{CancelReason}}</p>
                <p><a href="{{OrderUrl}}">View details &rarr;</a></p>
                """,
            availableVariables: ["OrderNumber", "BuyerName", "SellerStoreName", "CancelReason", "OrderUrl"]),

        new(
            templateKey: NotificationTemplateKeys.OrderCancelledSeller,
            displayName: "Order Cancelled — Seller",
            channel: NotificationChannel.Both,
            subjectTemplate: "Order #{{OrderNumber}} has been cancelled",
            bodyTemplate: """
                <p>Order <strong>#{{OrderNumber}}</strong> from <strong>{{BuyerName}}</strong> has been cancelled.</p>
                <p>Reason: {{CancelReason}}</p>
                <p><a href="{{OrderUrl}}">View details &rarr;</a></p>
                """,
            availableVariables: ["OrderNumber", "BuyerName", "SellerStoreName", "CancelReason", "OrderUrl"]),

        new(
            templateKey: NotificationTemplateKeys.DisputeOpenedSeller,
            displayName: "Dispute Opened — Seller",
            channel: NotificationChannel.Both,
            subjectTemplate: "A dispute has been opened for order #{{OrderNumber}}",
            bodyTemplate: """
                <p>A dispute has been opened by <strong>{{BuyerName}}</strong> for order <strong>#{{OrderNumber}}</strong>.</p>
                <p>Reason: {{DisputeReason}}</p>
                <p>Please respond by <strong>{{ResponseDeadline}}</strong>.</p>
                <p><a href="{{DisputeUrl}}">View dispute &rarr;</a></p>
                """,
            availableVariables: ["OrderNumber", "BuyerName", "DisputeReason", "DisputeUrl", "ResponseDeadline"]),

        new(
            templateKey: NotificationTemplateKeys.DisputeOpenedAdmin,
            displayName: "Dispute Opened — Admin",
            channel: NotificationChannel.Both,
            subjectTemplate: "New dispute for order #{{OrderNumber}}",
            bodyTemplate: """
                <p>New dispute opened by <strong>{{BuyerName}}</strong> for order <strong>#{{OrderNumber}}</strong>.</p>
                <p>Reason: {{DisputeReason}}</p>
                <p><a href="{{DisputeUrl}}">Review dispute &rarr;</a></p>
                """,
            availableVariables: ["OrderNumber", "BuyerName", "DisputeReason", "DisputeUrl", "ResponseDeadline"]),

        new(
            templateKey: NotificationTemplateKeys.DisputeRespondedBuyer,
            displayName: "Dispute Responded — Buyer",
            channel: NotificationChannel.Both,
            subjectTemplate: "Seller responded to your dispute for order #{{OrderNumber}}",
            bodyTemplate: """
                <p>The seller has responded to your dispute for order <strong>#{{OrderNumber}}</strong>.</p>
                <p><a href="{{DisputeUrl}}">View response &rarr;</a></p>
                """,
            availableVariables: ["OrderNumber", "DisputeUrl"]),

        new(
            templateKey: NotificationTemplateKeys.DisputeResolvedBuyer,
            displayName: "Dispute Resolved — Buyer",
            channel: NotificationChannel.Both,
            subjectTemplate: "Your dispute for order #{{OrderNumber}} has been resolved",
            bodyTemplate: """
                <p>Your dispute for order <strong>#{{OrderNumber}}</strong> has been resolved.</p>
                <p>Resolution: {{Resolution}}</p>
                <p><a href="{{DisputeUrl}}">View details &rarr;</a></p>
                """,
            availableVariables: ["OrderNumber", "Resolution", "DisputeUrl"]),

        new(
            templateKey: NotificationTemplateKeys.DisputeResolvedSeller,
            displayName: "Dispute Resolved — Seller",
            channel: NotificationChannel.Both,
            subjectTemplate: "Dispute for order #{{OrderNumber}} has been resolved",
            bodyTemplate: """
                <p>The dispute for order <strong>#{{OrderNumber}}</strong> has been resolved.</p>
                <p>Resolution: {{Resolution}}</p>
                <p><a href="{{DisputeUrl}}">View details &rarr;</a></p>
                """,
            availableVariables: ["OrderNumber", "Resolution", "DisputeUrl"]),

        new(
            templateKey: NotificationTemplateKeys.PayoutProcessedSeller,
            displayName: "Payout Processed — Seller",
            channel: NotificationChannel.Both,
            subjectTemplate: "Your payout of {{NetAmount}} has been processed",
            bodyTemplate: """
                <p>Hi {{SellerName}},</p>
                <p>Your payout has been processed:</p>
                <ul>
                  <li>Gross: {{GrossAmount}}</li>
                  <li>Commission: {{CommissionDeducted}}</li>
                  <li><strong>Net: {{NetAmount}}</strong></li>
                </ul>
                <p><a href="{{PayoutUrl}}">View payout details &rarr;</a></p>
                """,
            availableVariables: ["SellerName", "GrossAmount", "CommissionDeducted", "NetAmount", "PayoutUrl"]),

        new(
            templateKey: NotificationTemplateKeys.ReviewReceivedSeller,
            displayName: "Review Received — Seller",
            channel: NotificationChannel.Both,
            subjectTemplate: "New {{Rating}}-star review on {{ProductName}}",
            bodyTemplate: """
                <p><strong>{{ReviewerName}}</strong> left a <strong>{{Rating}}-star</strong> review on <strong>{{ProductName}}</strong>.</p>
                <p><a href="{{ReviewUrl}}">View review &rarr;</a></p>
                """,
            availableVariables: ["ProductName", "ReviewerName", "Rating", "ReviewUrl"]),

        new(
            templateKey: NotificationTemplateKeys.InventoryLowSeller,
            displayName: "Inventory Low — Seller",
            channel: NotificationChannel.InApp,
            subjectTemplate: null,
            bodyTemplate: """
                <p>Stock alert: <strong>{{ProductName}}</strong> ({{VariantName}}) is low — only <strong>{{CurrentStock}}</strong> remaining.</p>
                <p><a href="{{ProductUrl}}">Manage inventory &rarr;</a></p>
                """,
            availableVariables: ["ProductName", "VariantName", "CurrentStock", "ProductUrl"]),

        new(
            templateKey: NotificationTemplateKeys.PasswordResetRequest,
            displayName: "Password Reset Request",
            channel: NotificationChannel.Email,
            subjectTemplate: "Reset your MarketNest password",
            bodyTemplate: """
                <p>Hi {{UserName}},</p>
                <p>We received a request to reset your password. Click below to set a new password:</p>
                <p><a href="{{ResetUrl}}" style="display:inline-block;padding:12px 24px;background:#4f46e5;color:#fff;border-radius:6px;text-decoration:none">Reset Password</a></p>
                <p>This link expires in {{ExpiresIn}}.</p>
                <p>If you didn't request this, you can safely ignore this email.</p>
                """,
            availableVariables: ["UserName", "ResetUrl", "ExpiresIn"]),

        new(
            templateKey: NotificationTemplateKeys.NewLoginUnknownDevice,
            displayName: "New Login from Unknown Device",
            channel: NotificationChannel.Email,
            subjectTemplate: "New login to your MarketNest account",
            bodyTemplate: """
                <p>Hi {{UserName}},</p>
                <p>We detected a new login to your account:</p>
                <ul>
                  <li>Device: {{DeviceInfo}}</li>
                  <li>IP: {{IpAddress}}</li>
                  <li>Time: {{LoginTime}}</li>
                </ul>
                <p>If this wasn't you, please change your password immediately.</p>
                """,
            availableVariables: ["UserName", "DeviceInfo", "IpAddress", "LoginTime"])
    ];
}

