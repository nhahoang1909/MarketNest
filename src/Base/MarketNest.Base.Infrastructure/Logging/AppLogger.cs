using Microsoft.Extensions.Logging;

namespace MarketNest.Base.Infrastructure;

/// <summary>
/// ILogger&lt;T&gt; wrapper that implements IAppLogger&lt;T&gt;.
/// CA2254 is suppressed here intentionally — the template variability is by design
/// since this class is the single delegation point for all log calls.
/// </summary>
#pragma warning disable CA2254
internal sealed class AppLogger<T>(ILogger<T> inner) : IAppLogger<T>
{
    public bool IsEnabled(LogLevel level) => inner.IsEnabled(level);

    public void Trace(string message)                                         => inner.LogTrace(message);
    public void Trace(string template, params object?[] args)                 => inner.LogTrace(template, args);

    public void Debug(string message)                                         => inner.LogDebug(message);
    public void Debug(string template, params object?[] args)                 => inner.LogDebug(template, args);

    public void Info(string message)                                          => inner.LogInformation(message);
    public void Info(string template, params object?[] args)                  => inner.LogInformation(template, args);

    public void Warn(string message)                                          => inner.LogWarning(message);
    public void Warn(string template, params object?[] args)                  => inner.LogWarning(template, args);
    public void Warn(Exception ex, string message)                            => inner.LogWarning(ex, message);
    public void Warn(Exception ex, string template, params object?[] args)    => inner.LogWarning(ex, template, args);

    public void Error(Exception ex, string message)                           => inner.LogError(ex, message);
    public void Error(Exception ex, string template, params object?[] args)   => inner.LogError(ex, template, args);

    public void Critical(Exception ex, string message)                        => inner.LogCritical(ex, message);
    public void Critical(Exception ex, string template, params object?[] args) => inner.LogCritical(ex, template, args);
}
#pragma warning restore CA2254
