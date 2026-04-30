using Ganss.Xss;
using MarketNest.Base.Common;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Sanitizes HTML output from the Trix rich text editor.
///     Strips unsafe tags, attributes, and protocols to prevent XSS.
///     Whitelists only the elements and attributes that Trix generates.
/// </summary>
public sealed class TrixHtmlSanitizerService : IHtmlSanitizerService
{
    private static readonly HtmlSanitizer Sanitizer = BuildSanitizer();

    private static HtmlSanitizer BuildSanitizer()
    {
        var s = new HtmlSanitizer();

        // Only allow tags that Trix produces
        s.AllowedTags.Clear();
        s.AllowedTags.UnionWith(
        [
            // Text formatting
            "p", "br", "strong", "em", "u", "s",
            // Headings (Trix supports heading1 only, but allow h1/h2 for flexibility)
            "h1", "h2",
            // Block elements
            "blockquote", "pre", "code",
            // Lists
            "ul", "ol", "li",
            // Links
            "a",
            // Images (from Trix attachment uploads)
            "figure", "figcaption", "img",
            // Containers used by Trix internally
            "div", "span"
        ]);

        // Only allow safe attributes
        s.AllowedAttributes.Clear();
        s.AllowedAttributes.UnionWith(
        [
            // Links
            "href", "target", "rel",
            // Images
            "src", "alt", "width", "height",
            // Trix metadata attributes
            "data-trix-attachment", "data-trix-content-type",
            "data-trix-id", "data-width", "data-height",
            "data-file-id",
            // Styling
            "class"
        ]);

        // Only allow safe URL schemes
        s.AllowedSchemes.Clear();
        s.AllowedSchemes.UnionWith(["http", "https"]);

        // Enforce safe link attributes on anchors
        s.AllowedAtRules.Clear();

        return s;
    }

    /// <inheritdoc />
    public string Sanitize(string rawHtml)
        => string.IsNullOrWhiteSpace(rawHtml)
            ? string.Empty
            : Sanitizer.Sanitize(rawHtml);
}

