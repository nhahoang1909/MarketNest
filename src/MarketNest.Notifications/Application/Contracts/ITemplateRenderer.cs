namespace MarketNest.Notifications.Application;

/// <summary>
///     Renders notification template body/subject by replacing {{Variable}} placeholders.
/// </summary>
public interface ITemplateRenderer
{
    string Render(string template, IReadOnlyDictionary<string, string> variables);
}

