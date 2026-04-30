// tests/MarketNest.Analyzers.Tests/Architecture/HandlerQueryProjectionAnalyzerTests.cs
using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class HandlerQueryProjectionAnalyzerTests
{
    // ─── trigger tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task Triggers_ToListAsync_without_Select_in_QueryHandler()
    {
        var source = """
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading;
            using System.Threading.Tasks;
            interface IQueryHandler<TQ, TR> { }
            static class QueryableExtensions
            {
                public static Task<List<T>> ToListAsync<T>(this IQueryable<T> source, CancellationToken ct = default)
                    => Task.FromResult(source.ToList());
            }
            class ListOrdersQuery { }
            class ListOrdersQueryHandler : IQueryHandler<ListOrdersQuery, List<object>>
            {
                private IQueryable<object> _orders = null!;
                public Task<List<object>> Handle(ListOrdersQuery q, CancellationToken ct)
                    => _orders.Where(o => true).{|MN020:ToListAsync|}(ct);
            }
            """;
        await Verify<HandlerQueryProjectionAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_FirstOrDefaultAsync_without_Select_in_QueryHandler()
    {
        var source = """
            using System.Linq;
            using System.Threading;
            using System.Threading.Tasks;
            interface IQueryHandler<TQ, TR> { }
            static class QueryableExtensions
            {
                public static Task<T?> FirstOrDefaultAsync<T>(this IQueryable<T> src, CancellationToken ct = default)
                    => Task.FromResult(src.FirstOrDefault());
            }
            class GetOrderQuery { }
            class GetOrderQueryHandler : IQueryHandler<GetOrderQuery, object>
            {
                private IQueryable<object> _orders = null!;
                public Task<object?> Handle(GetOrderQuery q, CancellationToken ct)
                    => _orders.Where(o => true).{|MN020:FirstOrDefaultAsync|}(ct);
            }
            """;
        await Verify<HandlerQueryProjectionAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_ToListAsync_without_Select_in_BaseQuery_subclass()
    {
        var source = """
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading;
            using System.Threading.Tasks;
            abstract class BaseQuery<TEntity, TKey, TContext> { }
            static class QueryableExtensions
            {
                public static Task<List<T>> ToListAsync<T>(this IQueryable<T> source, CancellationToken ct = default)
                    => Task.FromResult(source.ToList());
            }
            class OrderReadDbContext { }
            class OrderQuery : BaseQuery<object, int, OrderReadDbContext>
            {
                private IQueryable<object> _orders = null!;
                public Task<List<object>> GetAllAsync(CancellationToken ct)
                    => _orders.Where(o => true).{|MN020:ToListAsync|}(ct);
            }
            """;
        await Verify<HandlerQueryProjectionAnalyzer>.AnalyzerAsync(source);
    }

    // ─── no-trigger tests ───────────────────────────────────────────────────

    [Fact]
    public async Task No_trigger_when_Select_precedes_ToListAsync_in_QueryHandler()
    {
        var source = """
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading;
            using System.Threading.Tasks;
            interface IQueryHandler<TQ, TR> { }
            static class QueryableExtensions
            {
                public static Task<List<T>> ToListAsync<T>(this IQueryable<T> source, CancellationToken ct = default)
                    => Task.FromResult(source.ToList());
            }
            class OrderDto { }
            class ListOrdersQuery { }
            class ListOrdersQueryHandler : IQueryHandler<ListOrdersQuery, List<OrderDto>>
            {
                private IQueryable<object> _orders = null!;
                public Task<List<OrderDto>> Handle(ListOrdersQuery q, CancellationToken ct)
                    => _orders.Where(o => true).Select(o => new OrderDto()).ToListAsync(ct);
            }
            """;
        await Verify<HandlerQueryProjectionAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_ToListAsync_without_Select_in_CommandHandler()
    {
        // CommandHandlers are intentionally excluded — they may need full entity loading
        var source = """
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading;
            using System.Threading.Tasks;
            interface ICommandHandler<TC, TR> { }
            static class QueryableExtensions
            {
                public static Task<List<T>> ToListAsync<T>(this IQueryable<T> source, CancellationToken ct = default)
                    => Task.FromResult(source.ToList());
            }
            class PlaceOrderCommand { }
            class PlaceOrderCommandHandler : ICommandHandler<PlaceOrderCommand, object>
            {
                private IQueryable<object> _orders = null!;
                public Task<List<object>> Validate(CancellationToken ct)
                    => _orders.Where(o => true).ToListAsync(ct);
            }
            """;
        await Verify<HandlerQueryProjectionAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_ToListAsync_outside_any_handler()
    {
        var source = """
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading;
            using System.Threading.Tasks;
            static class QueryableExtensions
            {
                public static Task<List<T>> ToListAsync<T>(this IQueryable<T> source, CancellationToken ct = default)
                    => Task.FromResult(source.ToList());
            }
            class SomeService
            {
                private IQueryable<object> _orders = null!;
                public Task<List<object>> GetAll(CancellationToken ct)
                    => _orders.Where(o => true).ToListAsync(ct);
            }
            """;
        await Verify<HandlerQueryProjectionAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_when_SelectMany_precedes_ToListAsync()
    {
        var source = """
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading;
            using System.Threading.Tasks;
            interface IQueryHandler<TQ, TR> { }
            static class QueryableExtensions
            {
                public static Task<List<T>> ToListAsync<T>(this IQueryable<T> source, CancellationToken ct = default)
                    => Task.FromResult(source.ToList());
            }
            class OrderItemDto { }
            class ListItemsQuery { }
            class ListItemsQueryHandler : IQueryHandler<ListItemsQuery, List<OrderItemDto>>
            {
                private IQueryable<object> _orders = null!;
                public Task<List<OrderItemDto>> Handle(ListItemsQuery q, CancellationToken ct)
                    => _orders.SelectMany(o => new[] { new OrderItemDto() }.AsQueryable())
                              .ToListAsync(ct);
            }
            """;
        await Verify<HandlerQueryProjectionAnalyzer>.AnalyzerAsync(source);
    }
}

