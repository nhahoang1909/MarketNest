using System.Text.RegularExpressions;
using MarketNest.Notifications.Application;

namespace MarketNest.Notifications.Infrastructure;

/// <summary>
///     Simple Handlebars-style template renderer. Replaces {{VariableName}} with dictionary values.
///     Leaves unreplaced variables intact (never crashes on missing variable).
/// </summary>
public sealed partial class HandlebarsTemplateRenderer : ITemplateRenderer
{
    public string Render(string template, IReadOnlyDictionary<string, string> variables)
    {
        return VariablePattern().Replace(template, match =>
        {
            // MN036: Group 1 is guaranteed by the regex pattern @"\{\{(\w+)\}\}"
#pragma warning disable MN036
            var key = match.Groups[1].Value;
#pragma warning restore MN036
            return variables.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}", RegexOptions.Compiled)]
    private static partial Regex VariablePattern();
}

