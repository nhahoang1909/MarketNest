using MediatR;

namespace MarketNest.Base.Common;

/// <summary>
///     Marker interface for commands. Commands change state and return Result&lt;T, Error&gt;.
/// </summary>
public interface ICommand<TResult> : IRequest<Result<TResult, Error>>;

/// <summary>
///     Convenience: command that returns Unit (void-equivalent).
/// </summary>
public interface ICommand : ICommand<Unit>;
