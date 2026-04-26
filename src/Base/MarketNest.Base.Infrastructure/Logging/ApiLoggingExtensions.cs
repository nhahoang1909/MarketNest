using System.Diagnostics;

namespace MarketNest.Base.Infrastructure;

public sealed class ApiLoggingScope<T> : IDisposable
{
    private readonly IAppLogger<T> _logger;
    private readonly string _apiName;
    private readonly string _correlationId;
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private bool _completed;

    public ApiLoggingScope(IAppLogger<T> logger, string apiName, object? payload = null, string correlationId = "-")
    {
        _logger = logger;
        _apiName = apiName;
        _correlationId = correlationId ?? "-";

        if (payload is null)
            _logger.Info("API {Api} Start - CorrelationId={Cid}", _apiName, _correlationId);
        else
            _logger.Info("API {Api} Start - CorrelationId={Cid} Payload={Payload}", _apiName, _correlationId, payload);
    }

    public void Success()
    {
        if (_completed) return;
        _sw.Stop();
        _logger.Info("API {Api} Success - ElapsedMs={ElapsedMs} CorrelationId={Cid}", _apiName, _sw.ElapsedMilliseconds, _correlationId);
        _completed = true;
    }

    public void Fail(Exception ex)
    {
        if (_completed) return;
        _sw.Stop();
        _logger.Error(ex, "API {Api} Error - ElapsedMs={ElapsedMs} CorrelationId={Cid}", _apiName, _sw.ElapsedMilliseconds, _correlationId);
        _completed = true;
    }

    public void Dispose()
    {
        if (!_completed)
        {
            _sw.Stop();
            _logger.Info("API {Api} End - ElapsedMs={ElapsedMs} CorrelationId={Cid}", _apiName, _sw.ElapsedMilliseconds, _correlationId);
            _completed = true;
        }
    }
}

public static class ApiLoggingExtensions
{
    public static ApiLoggingScope<T> BeginApiScope<T>(this IAppLogger<T> logger, string apiName, object? payload = null, string correlationId = "-")
        => new ApiLoggingScope<T>(logger, apiName, payload, correlationId);
}

