
namespace MarketNest.Base.Infrastructure;

/// <summary>
///     Marker interface for DI — extends ILogger so [LoggerMessage] delegates
///     can accept IAppLogger&lt;T&gt; directly.
///     Usage:
///     public class MyService(IAppLogger&lt;MyService&gt; logger) { ... }
///     Log.InfoSomething(logger, ...);
/// </summary>
public interface IAppLogger<T> : ILogger { }
