using System.Reflection;
using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;
// Audit attributes moved to Base.Common; use that namespace
using MediatR;

namespace MarketNest.Auditing.Infrastructure;

/// <summary>
///     MediatR pipeline behavior that automatically records audit entries for commands
///     decorated with <see cref="AuditedAttribute" />. Runs after the handler completes.
/// </summary>
public partial class AuditBehavior<TRequest, TResponse>(
    IAuditService auditService,
    IAppLogger<AuditBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        TResponse response = await next(cancellationToken);

        AuditedAttribute? attr = typeof(TRequest).GetCustomAttribute<AuditedAttribute>();
        if (attr is null)
            return response;

        try
        {
            bool isSuccess = IsSuccessResult(response);

            if (!isSuccess && !attr.AuditFailures)
                return response;

            string eventType = attr.EventType
                               ?? typeof(TRequest).Name.ToUpperInvariant().Replace("COMMAND", "");

            await auditService.RecordAsync(new AuditEntry
            {
                EventType = eventType,
                EntityType = attr.EntityType,
                EntityId = ExtractEntityId(request),
                Metadata = new Dictionary<string, string>
                {
                    ["success"] = isSuccess.ToString(),
                    ["request_type"] = typeof(TRequest).Name
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.ErrorAuditFailed(logger, typeof(TRequest).Name, ex);
        }

        return response;
    }

    private static bool IsSuccessResult(TResponse? response) =>
        response switch
        {
            null => false,
            _ when response.GetType().IsGenericType
                   && response.GetType().GetGenericTypeDefinition() == typeof(Result<,>)
                => (bool)(response.GetType().GetProperty(nameof(Result<int, int>.IsSuccess))?.GetValue(response) ??
                          false),
            _ => true
        };

    private static Guid? ExtractEntityId(TRequest request) =>
        request.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase))
            ?.GetValue(request) as Guid?;

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AuditBehaviorError, LogLevel.Error,
            "Failed to record audit for {RequestType}")]
        public static partial void ErrorAuditFailed(ILogger logger, string requestType, Exception ex);
    }
}
