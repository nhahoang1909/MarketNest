using System.Diagnostics;
using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;
using MediatR;

namespace MarketNest.Auditing.Infrastructure;

/// <summary>
///     MediatR pipeline behavior that measures elapsed time for every request
///     and logs a warning when the threshold defined in <see cref="SlaConstants.Performance" /> is exceeded.
///
///     Phase 1: structured log warnings only.
///     Phase 2: emit OpenTelemetry histogram metric for Prometheus/Grafana P95 computation.
///
///     Registration: call <c>AddAuditingModule()</c> — the behavior is registered there.
///     Position in pipeline: runs *before* <see cref="AuditBehavior{TRequest,TResponse}" /> so timing
///     includes the audit write time.
/// </summary>
public partial class PerformanceBehavior<TRequest, TResponse>(
    IAppLogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        Stopwatch sw = Stopwatch.StartNew();
        TResponse response = await next(cancellationToken);
        sw.Stop();

        long elapsedMs = sw.ElapsedMilliseconds;
        string requestName = typeof(TRequest).Name;

        if (elapsedMs >= SlaConstants.Performance.CriticalRequestMs)
        {
            Log.WarnCriticalSlowRequest(logger, requestName, elapsedMs,
                SlaConstants.Performance.CriticalRequestMs);
        }
        else if (elapsedMs >= SlaConstants.Performance.SlowRequestMs)
        {
            Log.WarnSlowRequest(logger, requestName, elapsedMs,
                SlaConstants.Performance.SlowRequestMs);
        }

        return response;
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.PerfBehaviorSlowRequest, LogLevel.Warning,
            "Slow request detected: {RequestName} took {ElapsedMs} ms (threshold: {ThresholdMs} ms)")]
        public static partial void WarnSlowRequest(
            ILogger logger, string requestName, long elapsedMs, int thresholdMs);

        [LoggerMessage((int)LogEventId.PerfBehaviorCriticalRequest, LogLevel.Warning,
            "CRITICAL slow request: {RequestName} took {ElapsedMs} ms (critical threshold: {ThresholdMs} ms) — SLA breach risk")]
        public static partial void WarnCriticalSlowRequest(
            ILogger logger, string requestName, long elapsedMs, int thresholdMs);
    }
}

