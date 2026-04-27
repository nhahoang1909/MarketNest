
namespace MarketNest.Base.Infrastructure;

/// <summary>
///     ILogger&lt;T&gt; wrapper that implements IAppLogger&lt;T&gt;.
///     Delegates all ILogger calls to the inner ILogger&lt;T&gt; via the core Log method
///     (not extension methods), so CA1848 does not fire.
/// </summary>
public sealed class AppLogger<T>(ILogger<T> inner) : IAppLogger<T>
{
    void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
        => inner.Log(logLevel, eventId, state, exception, formatter);

    bool ILogger.IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

    IDisposable? ILogger.BeginScope<TState>(TState state) => inner.BeginScope(state);
}
