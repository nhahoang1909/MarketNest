namespace MarketNest.Notifications.Infrastructure;

/// <summary>SMTP configuration options bound from appsettings.json.</summary>
public sealed class SmtpOptions
{
    public const string Section = "Smtp";

    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 1025;
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string FromAddress { get; init; } = "noreply@marketnest.com";
    public string FromName { get; init; } = "MarketNest";
    public bool UseSsl { get; init; }
}

