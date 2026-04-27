using MediatR;

namespace MarketNest.Base.Common;

/// <summary>
///     Marker interface for queries. Queries are read-only and NEVER change state.
///     No Result wrapper needed — return the DTO directly.
/// </summary>
public interface IQuery<TResult> : IRequest<TResult>;
