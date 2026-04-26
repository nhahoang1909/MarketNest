using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Development-only hosted service that auto-generates <c>docs/api-contract.md</c>
///     from the OpenAPI specification on application startup.
///     Ensures the markdown contract stays in sync with registered endpoints.
/// </summary>
public sealed class ApiContractGenerator : BackgroundService
{
    private const string RelativeOutputPath = "docs/api-contract.md";
    private const int StartupDelayMs = 3000;
    private readonly IWebHostEnvironment _environment;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiContractGenerator> _logger;

    private readonly IServiceProvider _serviceProvider;

    public ApiContractGenerator(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        IWebHostEnvironment environment,
        ILogger<ApiContractGenerator> logger)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _environment = environment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_environment.IsDevelopment())
            return;

        // Wait for the app to fully start so all endpoints are registered
        await Task.Delay(StartupDelayMs, stoppingToken);

        try
        {
            await GenerateContractAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // App shutting down — ignore
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to auto-generate api-contract.md");
        }
    }

    private async Task GenerateContractAsync(CancellationToken cancellationToken)
    {
        // Fetch the OpenAPI JSON from the local endpoint
        using HttpClient httpClient = _httpClientFactory.CreateClient();
        string serverUrl = GetServerUrl();
        string openApiUrl = string.Concat(serverUrl, "/openapi/", AppConstants.OpenApi.DocumentName, ".json");

        _logger.LogFetchingOpenApiSpec(openApiUrl);

        HttpResponseMessage response = await httpClient.GetAsync(openApiUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAPI endpoint returned {StatusCode} — skipping api-contract.md generation",
                response.StatusCode);
            return;
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        string markdown = GenerateMarkdown(doc.RootElement);
        string outputPath = ResolveOutputPath();

        string? directory = Path.GetDirectoryName(outputPath);
        if (directory is not null)
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(outputPath, markdown, Encoding.UTF8, cancellationToken);

        int endpointCount = CountEndpoints(doc.RootElement);
        _logger.LogApiContractUpdated(outputPath, endpointCount);
    }

    private string GetServerUrl()
    {
        // Resolve from Kestrel configuration or fall back to localhost:5000
        IConfiguration config = _serviceProvider.GetRequiredService<IConfiguration>();
        string urls = config["Urls"] ?? config["ASPNETCORE_URLS"] ?? "http://localhost:5000";
        return urls.Split(';')[0].TrimEnd('/').Replace("+", "localhost", StringComparison.Ordinal);
    }

    private string ResolveOutputPath()
    {
        // Navigate from src/MarketNest.Web/ up to solution root
        string solutionRoot = Path.GetFullPath(
            Path.Combine(_environment.ContentRootPath, "..", ".."));

        return Path.Combine(solutionRoot, RelativeOutputPath);
    }

    private static int CountEndpoints(JsonElement root)
    {
        if (!root.TryGetProperty("paths", out JsonElement paths))
            return 0;

        int count = 0;
        foreach (JsonProperty path in paths.EnumerateObject()) count += path.Value.EnumerateObject().Count();
        return count;
    }

    private static string GenerateMarkdown(JsonElement root)
    {
        var sb = new StringBuilder();

        // ── Header ───────────────────────────────────────────────────
        JsonElement info = root.TryGetProperty("info", out JsonElement infoEl) ? infoEl : default;
        string title = GetString(info, "title") ?? "MarketNest API";
        string version = GetString(info, "version") ?? "N/A";
        string? description = GetString(info, "description");

        sb.AppendLine("# MarketNest API Contract");
        sb.AppendLine();
        sb.AppendLine("> **Auto-generated** from OpenAPI specification on application startup (Development mode).");
        sb.AppendLine(
            "> Do NOT edit manually — changes will be overwritten. Add endpoints in code and restart the app.");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Version**: {version}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Title**: {title}");
        if (!string.IsNullOrWhiteSpace(description))
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Description**: {description}");
        sb.AppendLine();

        // ── Paths ────────────────────────────────────────────────────
        if (!root.TryGetProperty("paths", out JsonElement paths) || !paths.EnumerateObject().Any())
        {
            sb.AppendLine("_No endpoints registered yet._");
            return sb.ToString();
        }

        // Group by tag
        Dictionary<string, List<(string Path, string Method, JsonElement Operation)>> grouped = GroupByTag(paths);

        // ── Table of Contents ────────────────────────────────────────
        sb.AppendLine("## Table of Contents");
        sb.AppendLine();
        foreach (string tag in grouped.Keys)
        {
            string anchor = tag.ToLowerInvariant().Replace(' ', '-');
            sb.AppendLine(CultureInfo.InvariantCulture, $"- [{tag}](#{anchor})");
        }

        sb.AppendLine();

        // ── Endpoints by tag ─────────────────────────────────────────
        foreach ((string tag, List<(string Path, string Method, JsonElement Operation)> operations) in grouped)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"## {tag}");
            sb.AppendLine();

            foreach ((string path, string method, JsonElement operation) in operations)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"### `{method.ToUpperInvariant()} {path}`");
                sb.AppendLine();

                string? summary = GetString(operation, "summary");
                string? desc = GetString(operation, "description");
                string? operationId = GetString(operation, "operationId");

                if (!string.IsNullOrWhiteSpace(summary))
                    sb.AppendLine(CultureInfo.InvariantCulture, $"**Summary**: {summary}");
                if (!string.IsNullOrWhiteSpace(desc))
                    sb.AppendLine(CultureInfo.InvariantCulture, $"**Description**: {desc}");
                if (!string.IsNullOrWhiteSpace(operationId))
                    sb.AppendLine(CultureInfo.InvariantCulture, $"**Operation ID**: `{operationId}`");

                sb.AppendLine();

                // Parameters
                if (operation.TryGetProperty("parameters", out JsonElement parameters) &&
                    parameters.GetArrayLength() > 0)
                {
                    sb.AppendLine("**Parameters**:");
                    sb.AppendLine();
                    sb.AppendLine("| Name | In | Type | Required | Description |");
                    sb.AppendLine("|------|----|------|----------|-------------|");
                    foreach (JsonElement param in parameters.EnumerateArray())
                    {
                        string name = GetString(param, "name") ?? "—";
                        string inVal = GetString(param, "in") ?? "—";
                        string type = "string";
                        if (param.TryGetProperty("schema", out JsonElement schema))
                            type = GetString(schema, "type") ?? "string";
                        string required = param.TryGetProperty("required", out JsonElement req) && req.GetBoolean()
                            ? "✅"
                            : "❌";
                        string paramDesc = GetString(param, "description") ?? "—";
                        sb.AppendLine(CultureInfo.InvariantCulture,
                            $"| `{name}` | {inVal} | `{type}` | {required} | {paramDesc} |");
                    }

                    sb.AppendLine();
                }

                // Request body
                if (operation.TryGetProperty("requestBody", out JsonElement requestBody) &&
                    requestBody.TryGetProperty("content", out JsonElement content))
                {
                    sb.AppendLine("**Request Body**:");
                    sb.AppendLine();
                    foreach (JsonProperty ct in content.EnumerateObject())
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture, $"- Content-Type: `{ct.Name}`");
                        if (ct.Value.TryGetProperty("schema", out JsonElement s) &&
                            s.TryGetProperty("$ref", out JsonElement refVal))
                        {
                            string refName = refVal.GetString()?.Split('/').LastOrDefault() ?? "—";
                            sb.AppendLine(CultureInfo.InvariantCulture, $"  - Schema: `{refName}`");
                        }
                    }

                    sb.AppendLine();
                }

                // Responses
                if (operation.TryGetProperty("responses", out JsonElement responses) &&
                    responses.EnumerateObject().Any())
                {
                    sb.AppendLine("**Responses**:");
                    sb.AppendLine();
                    sb.AppendLine("| Status | Description |");
                    sb.AppendLine("|--------|-------------|");
                    foreach (JsonProperty resp in responses.EnumerateObject())
                    {
                        string respDesc = GetString(resp.Value, "description") ?? "—";
                        sb.AppendLine(CultureInfo.InvariantCulture, $"| `{resp.Name}` | {respDesc} |");
                    }

                    sb.AppendLine();
                }

                sb.AppendLine("---");
                sb.AppendLine();
            }
        }

        // ── Schemas ──────────────────────────────────────────────────
        if (root.TryGetProperty("components", out JsonElement components) &&
            components.TryGetProperty("schemas", out JsonElement schemas) &&
            schemas.EnumerateObject().Any())
        {
            sb.AppendLine("## Schemas");
            sb.AppendLine();

            foreach (JsonProperty schema in schemas.EnumerateObject())
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"### `{schema.Name}`");
                sb.AppendLine();

                if (schema.Value.TryGetProperty("properties", out JsonElement props) &&
                    props.EnumerateObject().Any())
                {
                    var requiredProps = new HashSet<string>(StringComparer.Ordinal);
                    if (schema.Value.TryGetProperty("required", out JsonElement reqArr))
                        foreach (JsonElement r in reqArr.EnumerateArray())
                        {
                            string? name = r.GetString();
                            if (name is not null) requiredProps.Add(name);
                        }

                    sb.AppendLine("| Property | Type | Required | Description |");
                    sb.AppendLine("|----------|------|----------|-------------|");
                    foreach (JsonProperty prop in props.EnumerateObject())
                    {
                        string type = GetString(prop.Value, "type") ?? "object";
                        if (prop.Value.TryGetProperty("$ref", out JsonElement refVal))
                            type = refVal.GetString()?.Split('/').LastOrDefault() ?? "object";
                        string isRequired = requiredProps.Contains(prop.Name) ? "✅" : "❌";
                        string propDesc = GetString(prop.Value, "description") ?? "—";
                        sb.AppendLine(CultureInfo.InvariantCulture,
                            $"| `{prop.Name}` | `{type}` | {isRequired} | {propDesc} |");
                    }
                }

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static Dictionary<string, List<(string Path, string Method, JsonElement Operation)>> GroupByTag(
        JsonElement paths)
    {
        const string defaultTag = "Untagged";
        var groups = new Dictionary<string, List<(string, string, JsonElement)>>(StringComparer.OrdinalIgnoreCase);

        foreach (JsonProperty pathEntry in paths.EnumerateObject())
        foreach (JsonProperty methodEntry in pathEntry.Value.EnumerateObject())
        {
            string tag = defaultTag;
            if (methodEntry.Value.TryGetProperty("tags", out JsonElement tags) &&
                tags.GetArrayLength() > 0)
                tag = tags[0].GetString() ?? defaultTag;

            if (!groups.TryGetValue(tag, out List<(string, string, JsonElement)>? list))
            {
                list = [];
                groups[tag] = list;
            }

            list.Add((pathEntry.Name, methodEntry.Name, methodEntry.Value));
        }

        return groups;
    }

    private static string? GetString(JsonElement element, string property)
    {
        // Defensive: some OpenAPI properties may be arrays/objects/numbers/booleans
        // (for example `tags` is an array). Return a readable string when possible
        // instead of calling GetString() which throws for non-string kinds.
        if (element.ValueKind == JsonValueKind.Undefined || element.ValueKind == JsonValueKind.Null)
            return null;

        if (!element.TryGetProperty(property, out JsonElement val))
            return null;

        switch (val.ValueKind)
        {
            case JsonValueKind.String:
                return val.GetString();
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                // Return the raw JSON token for simple scalars
                return val.GetRawText();
            case JsonValueKind.Array:
            {
                // If it's an array of strings, join them; otherwise return raw JSON
                var parts = new List<string>();
                foreach (JsonElement item in val.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        parts.Add(item.GetString() ?? string.Empty);
                    else
                        parts.Add(item.GetRawText());
                }

                return string.Join(", ", parts.Where(s => !string.IsNullOrEmpty(s)));
            }
            case JsonValueKind.Object:
                // Serialize object to compact JSON for display
                return val.GetRawText();
            default:
                return null;
        }
    }
}

internal static partial class ApiContractGeneratorLogMessages
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Fetching OpenAPI spec from {Url}")]
    public static partial void LogFetchingOpenApiSpec(this ILogger logger, string url);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "api-contract.md updated at {Path} with {EndpointCount} endpoints")]
    public static partial void LogApiContractUpdated(this ILogger logger, string path, int endpointCount);
}
