using MediatR;

namespace MarketNest.Base.Common;

/// <summary>
///     Handler for commands. Always returns Result&lt;T, Error&gt;.
/// </summary>
public interface ICommandHandler<TCommand, TResult>
    : IRequestHandler<TCommand, Result<TResult, Error>>
    where TCommand : ICommand<TResult>;
