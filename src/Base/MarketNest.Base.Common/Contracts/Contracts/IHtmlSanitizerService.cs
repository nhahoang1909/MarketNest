namespace MarketNest.Base.Common;

/// <summary>
///     Sanitizes untrusted HTML (e.g. from rich text editors) before persistence.
///     Strips unsafe tags, attributes, and protocols to prevent XSS.
/// </summary>
public interface IHtmlSanitizerService
{
    /// <summary>
    ///     Returns sanitized HTML with only whitelisted tags and attributes.
    ///     Returns <see cref="string.Empty"/> for null or whitespace input.
    /// </summary>
    string Sanitize(string rawHtml);
}

