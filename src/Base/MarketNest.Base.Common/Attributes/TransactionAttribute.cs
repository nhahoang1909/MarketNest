using System.Data;

namespace MarketNest.Base.Common;

/// <summary>
///     Marks a controller class or action method (or Razor Page handler) to be wrapped
///     in a database transaction. The <c>RazorPageTransactionFilter</c> and
///     <c>TransactionActionFilter</c> in <c>MarketNest.Web</c> honour this attribute.
///
///     On Razor Pages, <c>OnPost*</c> handlers are wrapped automatically by convention;
///     use this attribute only to override the default isolation level or timeout.
///     On API controllers, annotate the controller class (or individual actions) to
///     enable transaction wrapping.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TransactionAttribute(
    IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
    int timeoutSeconds = 30)
    : Attribute
{
    public IsolationLevel IsolationLevel { get; } = isolationLevel;
    public int TimeoutSeconds { get; } = timeoutSeconds;
}

/// <summary>
///     Opt-out marker — prevents <c>RazorPageTransactionFilter</c> or
///     <c>TransactionActionFilter</c> from wrapping a specific handler or action in a
///     transaction, even when the class-level <c>[Transaction]</c> attribute is present.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class NoTransactionAttribute : Attribute
{
}

