namespace MarketNest.Notifications.Application;

/// <summary>Wraps rendered notification content in branded email HTML layout.</summary>
public interface IEmailLayoutRenderer
{
    string Wrap(string renderedContent, string baseUrl);
}

