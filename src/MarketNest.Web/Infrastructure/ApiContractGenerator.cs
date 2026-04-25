using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MarketNest.Web.Infrastructure;

/// <summary>
/// Development-only hosted service that auto-generates <c>docs/api-contract.md</c>
/// from the OpenAPI specification on application startup.
/// Ensures the markdown contract stays in sync with registered endpoints.
/// </summary>
public sealed partial class ApiContractGenerator : BackgroundService
{
    private const string RelativeOutputPath = "docs/api-contract.md";
    private const int StartupDelayMs = 3000;

    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ApiContractGenerator> _logger;

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
        using var httpClient = _httpClientFactory.CreateClient();
        var serverUrl = GetServerUrl();
        var openApiUrl = string.Concat(serverUrl, "/openapi/", AppConstants.OpenApi.DocumentName, ".json");

        _logger.LogFetchingOpenApiSpec(openApiUrl);

        var response = await httpClient.GetAsync(openApiUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAPI endpoint returned {StatusCode} — skipping api-contract.md generation",
                response.StatusCode);
            return;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        var markdown = GenerateMarkdown(doc.RootElement);
        var outputPath = ResolveOutputPath();

        var directory = Path.GetDirectoryName(outputPath);
        if (directory is not null)
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(outputPath, markdown, Encoding.UTF8, cancellationToken);

        var endpointCount = CountEndpoints(doc.RootElement);
        _logger.LogApiContractUpdated(outputPath, endpointCount);
    }

    private string GetServerUrl()
    {
        // Resolve from Kestrel configuration or fall back to localhost:5000
        var config = _serviceProvider.GetRequiredService<IConfiguration>();
        var urls = config["Urls"] ?? config["ASPNETCORE_URLS"] ?? "http://localhost:5000";
        return urls.Split(';')[0].TrimEnd('/');
    }

    private string ResolveOutputPath()
    {
        // Navigate from src/MarketNest.Web/ up to solution root
        var solutionRoot = Path.GetFullPath(
            Path.Combine(_environment.ContentRootPath, "..", ".."));

        return Path.Combine(solutionRoot, RelativeOutputPath);
    }

    private static int CountEndpoints(JsonElement root)
    {
        if (!root.TryGetProperty("paths", out var paths))
            return 0;

        var count = 0;
        foreach (var path in paths.EnumerateObject())
        {
            count += path.Value.EnumerateObject().Count();
        }
        return count;
    }

    private static string GenerateMarkdown(JsonElement root)
    {
        var sb = new StringBuilder();

        // ── Header ───────────────────────────────────────────────────
        var info = root.TryGetProperty("info", out var infoEl) ? infoEl : default;
        var title = GetString(info, "title") ?? "MarketNest API";
        var version = GetString(info, "version") ?? "N/A";
        var description = GetString(info, "description");

        sb.AppendLine("# MarketNest API Contract");
        sb.AppendLine();
        sb.AppendLine("> **Auto-generated** from OpenAPI specification on application startup (Development mode).");
        sb.AppendLine("> Do NOT edit manually — changes will be overwritten. Add endpoints in code and restart the app.");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Version**: {version}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Title**: {title}");
        if (!string.IsNullOrWhiteSpace(description))
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Description**: {description}");
        sb.AppendLine();

        // ── Paths ────────────────────────────────────────────────────
        if (!root.TryGetProperty("paths", out var paths) || !paths.EnumerateObject().Any())
        {
            sb.AppendLine("_No endpoints registered yet._");
            return sb.ToString();
        }

        // Group by tag
        var grouped = GroupByTag(paths);

        // ── Table of Contents ────────────────────────────────────────
        sb.AppendLine("## Table of Contents");
        sb.AppendLine();
        foreach (var tag in grouped.Keys)
        {
            var anchor = tag.ToLowerInvariant().Replace(' ', '-');
            sb.AppendLine(CultureInfo.InvariantCulture, $"- [{tag}](#{anchor})");
        }
        sb.AppendLine();

        // ── Endpoints by tag ─────────────────────────────────────────
        foreach (var (tag, operations) in grouped)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"## {tag}");
            sb.AppendLine();

            foreach (var (path, method, operation) in operations)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"### `{method.ToUpperInvariant()} {path}`");
                sb.AppendLine();

                var summary = GetString(operation, "summary");
                var desc = GetString(operation, "description");
                var operationId = GetString(operation, "operationId");

                if (!string.IsNullOrWhiteSpace(summary))
                    sb.AppendLine(CultureInfo.InvariantCulture, $"**Summary**: {summary}");
                if (!string.IsNullOrWhiteSpace(desc))
                    sb.AppendLine(CultureInfo.InvariantCulture, $"**Description**: {desc}");
                if (!string.IsNullOrWhiteSpace(operationId))
                    sb.AppendLine(CultureInfo.InvariantCulture, $"**Operation ID**: `{operationId}`");

                sb.AppendLine();

                // Parameters
                if (operation.TryGetProperty("parameters", out var parameters) &&
                    parameters.GetArrayLength() > 0)
                {
                    sb.AppendLine("**Parameters**:");
                    sb.AppendLine();
                    sb.AppendLine("| Name | In | Type | Required | Description |");
                    sb.AppendLine("|------|----|------|----------|-------------|");
                    foreach (var param in parameters.EnumerateArray())
                    {
                        var name = GetString(param, "name") ?? "—";
                        var inVal = GetString(param, "in") ?? "—";
                        var type = "string";
                        if (param.TryGetProperty("schema", out var schema))
                            type = GetString(schema, "type") ?? "string";
                        var required = param.TryGetProperty("required", out var req) && req.GetBoolean() ? "✅" : "❌";
                        var paramDesc = GetString(param, "description") ?? "—";
                        sb.AppendLine(CultureInfo.InvariantCulture, $"| `{name}` | {inVal} | `{type}` | {required} | {paramDesc} |");
                    }
                    sb.AppendLine();
                }

                // Request body
                if (operation.TryGetProperty("requestBody", out var requestBody) &&
                    requestBody.TryGetProperty("content", out var content))
                {
                    sb.AppendLine("**Request Body**:");
                    sb.AppendLine();
                    foreach (var ct in content.EnumerateObject())
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture, $"- Content-Type: `{ct.Name}`");
                        if (ct.Value.TryGetProperty("schema", out var s) &&
                            s.TryGetProperty("$ref", out var refVal))
                        {
                            var refName = refVal.GetString()?.Split('/').LastOrDefault() ?? "—";
                            sb.AppendLine(CultureInfo.InvariantCulture, $"  - Schema: `{refName}`");
                        }
                    }
                    sb.AppendLine();
                }

                // Responses
                if (operation.TryGetProperty("responses", out var responses) &&
                    responses.EnumerateObject().Any())
                {
                    sb.AppendLine("**Responses**:");
                    sb.AppendLine();
                    sb.AppendLine("| Status | Description |");
                    sb.AppendLine("|--------|-------------|");
                    foreach (var resp in responses.EnumerateObject())
                    {
                        var respDesc = GetString(resp.Value, "description") ?? "—";
                        sb.AppendLine(CultureInfo.InvariantCulture, $"| `{resp.Name}` | {respDesc} |");
                    }
                    sb.AppendLine();
                }

                sb.AppendLine("---");
                sb.AppendLine();
            }
        }

        // ── Schemas ──────────────────────────────────────────────────
        if (root.TryGetProperty("components", out var components) &&
            components.TryGetProperty("schemas", out var schemas) &&
            schemas.EnumerateObject().Any())
        {
            sb.AppendLine("## Schemas");
            sb.AppendLine();

            foreach (var schema in schemas.EnumerateObject())
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"### `{schema.Name}`");
                sb.AppendLine();

                if (schema.Value.TryGetProperty("properties", out var props) &&
                    props.EnumerateObject().Any())
                {
                    var requiredProps = new HashSet<string>(StringComparer.Ordinal);
                    if (schema.Value.TryGetProperty("required", out var reqArr))
                    {
                        foreach (var r in reqArr.EnumerateArray())
                        {
                            var name = r.GetString();
                            if (name is not null) requiredProps.Add(name);
                        }
                    }

                    sb.AppendLine("| Property | Type | Required | Description |");
                    sb.AppendLine("|----------|------|----------|-------------|");
                    foreach (var prop in props.EnumerateObject())
                    {
                        var type = GetString(prop.Value, "type") ?? "object";
                        if (prop.Value.TryGetProperty("$ref", out var refVal))
                            type = refVal.GetString()?.Split('/').LastOrDefault() ?? "object";
                        var isRequired = requiredProps.Contains(prop.Name) ? "✅" : "❌";
                        var propDesc = GetString(prop.Value, "description") ?? "—";
                        sb.AppendLine(CultureInfo.InvariantCulture, $"| `{prop.Name}` | `{type}` | {isRequired} | {propDesc} |");
                    }
                }

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static Dictionary<string, List<(string Path, string Method, JsonElement Operation)>> GroupByTag(JsonElement paths)
    {
        const string defaultTag = "Untagged";
        var groups = new Dictionary<string, List<(string, string, JsonElement)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var pathEntry in paths.EnumerateObject())
        {
            foreach (var methodEntry in pathEntry.Value.EnumerateObject())
            {
                var tag = defaultTag;
                if (methodEntry.Value.TryGetProperty("tags", out var tags) &&
                    tags.GetArrayLength() > 0)
                {
                    tag = tags[0].GetString() ?? defaultTag;
                }

                if (!groups.TryGetValue(tag, out var list))
                {
                    list = [];
                    groups[tag] = list;
                }

                list.Add((pathEntry.Name, methodEntry.Name, methodEntry.Value));
            }
        }

        return groups;
    }

    private static string? GetString(JsonElement element, string property)
    {
        if (element.ValueKind == JsonValueKind.Undefined)
            return null;
        return element.TryGetProperty(property, out var val) ? val.GetString() : null;
    }
}

internal static partial class ApiContractGeneratorLogMessages
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Fetching OpenAPI spec from {Url}")]
    public static partial void LogFetchingOpenApiSpec(this ILogger logger, string url);

    [LoggerMessage(Level = LogLevel.Information, Message = "api-contract.md updated at {Path} with {EndpointCount} endpoints")]
    public static partial void LogApiContractUpdated(this ILogger logger, string path, int endpointCount);
}

