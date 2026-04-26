
namespace MarketNest.Base.Infrastructure;

/// <summary>
///     ILogger&lt;T&gt; wrapper that implements IAppLogger&lt;T&gt;.
///     CA2254 and CA1848 are suppressed here intentionally — the template variability is by design
///     since this class is the single delegation point for all log calls.
/// </summary>
#pragma warning disable CA2254, CA1848
public sealed class AppLogger<T>(ILogger<T> inner) : IAppLogger<T>
{
    public bool IsEnabled(LogLevel level) => inner.IsEnabled(level);

    public void Trace(string message) => inner.LogTrace(message);
    public void Trace(string messageTemplate, params object?[] args) => inner.LogTrace(messageTemplate, args);

    public void Debug(string message) => inner.LogDebug(message);
    public void Debug(string messageTemplate, params object?[] args) => inner.LogDebug(messageTemplate, args);

    public void Info(string message) => inner.LogInformation(message);
    public void Info(string messageTemplate, params object?[] args) => inner.LogInformation(messageTemplate, args);

    public void Warn(string message) => inner.LogWarning(message);
    public void Warn(string messageTemplate, params object?[] args) => inner.LogWarning(messageTemplate, args);
    public void Warn(Exception ex, string message) => inner.LogWarning(ex, message);
    public void Warn(Exception ex, string messageTemplate, params object?[] args) => inner.LogWarning(ex, messageTemplate, args);

#pragma warning disable CA1716
    public void Error(Exception ex, string message) => inner.LogError(ex, message);
    public void Error(Exception ex, string messageTemplate, params object?[] args) => inner.LogError(ex, messageTemplate, args);
#pragma warning restore CA1716

    public void Critical(Exception ex, string message) => inner.LogCritical(ex, message);
    public void Critical(Exception ex, string messageTemplate, params object?[] args) => inner.LogCritical(ex, messageTemplate, args);
}
#pragma warning restore CA2254, CA1848
