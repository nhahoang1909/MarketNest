using System.Diagnostics.CodeAnalysis;

// IDomainEventHandler intentionally ends in "EventHandler" — it IS a handler for domain events.
[assembly: SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "IDomainEventHandler is an explicit domain pattern name.",
    Scope = "type", Target = "~T:MarketNest.Core.Common.Events.IDomainEventHandler`1")]

// Static factory methods on Result<TValue,TError> — callers use the non-generic Result static class;
// these methods are needed internally for Map/MapAsync chaining.
[assembly: SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
    Justification = "Static factories on Result<TValue,TError> are required for internal Map/MapAsync chaining.",
    Scope = "member", Target = "~M:MarketNest.Core.Common.Result`2.Success(`0)~MarketNest.Core.Common.Result`2")]
[assembly: SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
    Justification = "Static factories on Result<TValue,TError> are required for internal Map/MapAsync chaining.",
    Scope = "member", Target = "~M:MarketNest.Core.Common.Result`2.Failure(`1)~MarketNest.Core.Common.Result`2")]

// PagedResult<T>.Empty is a convenience factory; a non-generic PagedResult class would add
// unnecessary boilerplate for a single method.
[assembly: SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
    Justification = "PagedResult<T>.Empty is a convenience factory with no better alternative without extra boilerplate.",
    Scope = "member", Target = "~M:MarketNest.Core.Common.Queries.PagedResult`1.Empty(System.Int32,System.Int32)~MarketNest.Core.Common.Queries.PagedResult`1")]
