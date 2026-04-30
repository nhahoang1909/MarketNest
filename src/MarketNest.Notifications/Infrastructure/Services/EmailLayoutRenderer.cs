using MarketNest.Notifications.Application;

namespace MarketNest.Notifications.Infrastructure;

/// <summary>
///     Wraps rendered email body content in a consistent branded HTML layout.
///     Admin cannot modify this layout — only the inner body content is editable.
/// </summary>
public sealed class EmailLayoutRenderer : IEmailLayoutRenderer
{
    private const string Layout = """
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"><title>MarketNest</title></head>
        <body style="font-family:sans-serif;max-width:600px;margin:0 auto;padding:20px">
          <div style="text-align:center;margin-bottom:24px">
            <img src="{{BaseUrl}}/images/logo.png" height="40" alt="MarketNest" />
          </div>
          <div style="background:#fff;border-radius:8px;padding:32px;border:1px solid #e5e7eb">
            {{CONTENT}}
          </div>
          <div style="text-align:center;margin-top:24px;color:#9ca3af;font-size:12px">
            <p>&copy; 2026 MarketNest. All rights reserved.</p>
          </div>
        </body>
        </html>
        """;

    public string Wrap(string renderedContent, string baseUrl)
        => Layout
            .Replace("{{CONTENT}}", renderedContent)
            .Replace("{{BaseUrl}}", baseUrl);
}

