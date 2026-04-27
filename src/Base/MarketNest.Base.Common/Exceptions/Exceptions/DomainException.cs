namespace MarketNest.Base.Common;

/// <summary>
///     Thrown for domain invariant violations that should never happen if validation is correct.
///     Not for user input errors — those use Result&lt;T, Error&gt;.
/// </summary>
public class DomainException(string message) : Exception(message);
