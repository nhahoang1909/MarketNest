using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MarketNest.Notifications.Application;

namespace MarketNest.Notifications.Infrastructure;

/// <summary>
///     Sends emails via SMTP using MailKit.
///     Phase 1: connects to MailHog (localhost:1025, no auth).
///     Production: configure real SMTP credentials in appsettings.
/// </summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mimeMessage.To.Add(MailboxAddress.Parse(message.To));
        mimeMessage.Subject = message.Subject;

        var bodyBuilder = new BodyBuilder { HtmlBody = message.HtmlBody };
        if (message.PlainTextBody is not null)
            bodyBuilder.TextBody = message.PlainTextBody;

        mimeMessage.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        var secureOption = _options.UseSsl
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        await client.ConnectAsync(_options.Host, _options.Port, secureOption, ct);

        if (!string.IsNullOrEmpty(_options.Username))
            await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, ct);

        await client.SendAsync(mimeMessage, ct);
        await client.DisconnectAsync(quit: true, ct);
    }
}

