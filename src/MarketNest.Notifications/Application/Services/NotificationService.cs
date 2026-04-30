using System.Text.RegularExpressions;
using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;
using MarketNest.Notifications.Domain;

namespace MarketNest.Notifications.Application;

/// <summary>
///     Central notification dispatch service. Loads template, renders content,
///     dispatches to email and/or in-app channels based on template configuration.
/// </summary>
public sealed partial class NotificationService(
    INotificationTemplateRepository templateRepository,
    ITemplateRenderer renderer,
    IEmailSender emailSender,
    INotificationRepository notificationRepository,
    IEmailLayoutRenderer layoutRenderer,
    IAppLogger<NotificationService> logger) : INotificationService
{
    private const string DefaultBaseUrl = "http://localhost:5000";

    public async Task SendAsync(
        Guid recipientUserId,
        string templateKey,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken ct = default)
    {
        var template = await templateRepository.GetByKeyAsync(templateKey, ct);
        if (template is null || !template.IsActive)
        {
            Log.TemplateNotFoundOrInactive(logger, templateKey);
            return;
        }

        var renderedBody = renderer.Render(template.BodyTemplate, variables);
        var renderedSubject = template.SubjectTemplate is not null
            ? renderer.Render(template.SubjectTemplate, variables)
            : templateKey;

        // In-App notification
        if (template.Channel is NotificationChannel.InApp or NotificationChannel.Both)
        {
            var notification = new Notification(
                userId: recipientUserId,
                templateKey: templateKey,
                title: renderedSubject,
                body: StripHtml(renderedBody, maxLength: 300),
                actionUrl: variables.GetValueOrDefault("OrderUrl")
                           ?? variables.GetValueOrDefault("DisputeUrl")
                           ?? variables.GetValueOrDefault("PayoutUrl")
                           ?? variables.GetValueOrDefault("ProductUrl")
                           ?? variables.GetValueOrDefault("ReviewUrl"));

            notificationRepository.Add(notification);
        }

        // Email — Phase 1: always RealTime (no digest logic yet)
        if (template.Channel is NotificationChannel.Email or NotificationChannel.Both)
        {
            // TODO: Resolve user email from Identity module via cross-module contract
            // For Phase 1, email dispatch is deferred until Identity exposes IUserEmailResolver
            Log.EmailDispatchDeferred(logger, templateKey, recipientUserId);
        }
    }

    public async Task SendToMultipleAsync(
        IEnumerable<Guid> recipientUserIds,
        string templateKey,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken ct = default)
    {
        foreach (var userId in recipientUserIds)
            await SendAsync(userId, templateKey, variables, ct);
    }

    public async Task SendSecurityEmailAsync(
        string toEmail,
        string templateKey,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken ct = default)
    {
        var template = await templateRepository.GetByKeyAsync(templateKey, ct);
        if (template is null)
        {
            Log.TemplateNotFoundOrInactive(logger, templateKey);
            return;
        }

        var renderedBody = renderer.Render(template.BodyTemplate, variables);
        var renderedSubject = template.SubjectTemplate is not null
            ? renderer.Render(template.SubjectTemplate, variables)
            : "Security Alert";

        var wrappedHtml = layoutRenderer.Wrap(renderedBody, DefaultBaseUrl);
        var email = new EmailMessage(toEmail, renderedSubject, wrappedHtml);

        try
        {
            await emailSender.SendAsync(email, ct);
            Log.SecurityEmailSent(logger, templateKey, toEmail);
        }
        catch (Exception ex)
        {
            Log.SecurityEmailFailed(logger, templateKey, toEmail, ex);
        }
    }

    private static string StripHtml(string html, int maxLength)
    {
        var text = HtmlTagPattern().Replace(html, string.Empty).Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagPattern();

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "Notification template '{TemplateKey}' not found or inactive")]
        public static partial void TemplateNotFoundOrInactive(ILogger logger, string templateKey);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Email dispatch deferred for template '{TemplateKey}' to user {UserId}")]
        public static partial void EmailDispatchDeferred(ILogger logger, string templateKey, Guid userId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Security email sent: template '{TemplateKey}' to {Email}")]
        public static partial void SecurityEmailSent(ILogger logger, string templateKey, string email);

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send security email: template '{TemplateKey}' to {Email}")]
        public static partial void SecurityEmailFailed(ILogger logger, string templateKey, string email, Exception ex);
    }
}

