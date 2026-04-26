using MediatR;

namespace MarketNest.Core.Common.Cqrs;

/// <summary>
///     Handler for queries. Returns TResult directly (no Result wrapper).
/// </summary>
public interface IQueryHandler<TQuery, TResult>
    : IRequestHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>;
