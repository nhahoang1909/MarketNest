using Microsoft.Extensions.Logging;

namespace MarketNest.Core.Logging;

/// <summary>
/// Thin logging abstraction used across all MarketNest modules.
/// Wraps ILogger with short method names: Info, Debug, Trace, Warn, Error, Critical.
///
/// Usage in a class:
///   public class OrderRepository(IAppLogger&lt;OrderRepository&gt; logger) { ... }
///
///   _logger.Info("Order {Id} placed", orderId);
///   _logger.Warn(ex, "Payment timeout after {Ms}ms", elapsed);
///   _logger.Error(ex, "Failed to save order {Id}", orderId);
/// </summary>
public interface IAppLogger<T>
{
    bool IsEnabled(LogLevel level);

    void Trace(string message);
    void Trace(string template, params object?[] args);

    void Debug(string message);
    void Debug(string template, params object?[] args);

    void Info(string message);
    void Info(string template, params object?[] args);

    void Warn(string message);
    void Warn(string template, params object?[] args);
    void Warn(Exception ex, string message);
    void Warn(Exception ex, string template, params object?[] args);

    void Error(Exception ex, string message);
    void Error(Exception ex, string template, params object?[] args);

    void Critical(Exception ex, string message);
    void Critical(Exception ex, string template, params object?[] args);
}
