using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class QueryHandlerNoTrackingAnalyzerTests
{
    [Fact]
    public async Task Triggers_when_query_handler_materialises_without_AsNoTracking()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using System.Collections.Generic;
            using System.Linq;
            interface IQueryHandler<TQuery, TResult> {
                Task<TResult> Handle(TQuery query, CancellationToken ct);
            }
            record GetOrdersQuery();
            class DbSet<T> : IQueryable<T> {
                public System.Type ElementType => typeof(T);
                public System.Linq.Expressions.Expression Expression => null!;
                public IQueryProvider Provider => null!;
                public IEnumerator<T> GetEnumerator() => null!;
                System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => null!;
            }
            static class Ext {
                public static Task<List<T>> ToListAsync<T>(this IQueryable<T> q, CancellationToken ct = default) => Task.FromResult(new List<T>());
            }
            class OrderDto { }
            class Db { public DbSet<OrderDto> Orders { get; } = new(); }
            class MyHandler : IQueryHandler<GetOrdersQuery, List<OrderDto>> {
                private Db _db = new();
                public async Task<List<OrderDto>> Handle(GetOrdersQuery query, CancellationToken ct) {
                    return await _db.Orders.{|MN029:ToListAsync|}(ct);
                }
            }
            """;
        await Verify<QueryHandlerNoTrackingAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_when_AsNoTracking_is_present()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using System.Collections.Generic;
            using System.Linq;
            interface IQueryHandler<TQuery, TResult> {
                Task<TResult> Handle(TQuery query, CancellationToken ct);
            }
            record GetOrdersQuery();
            class DbSet<T> : IQueryable<T> {
                public System.Type ElementType => typeof(T);
                public System.Linq.Expressions.Expression Expression => null!;
                public IQueryProvider Provider => null!;
                public IEnumerator<T> GetEnumerator() => null!;
                System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => null!;
            }
            static class Ext {
                public static IQueryable<T> AsNoTracking<T>(this IQueryable<T> q) => q;
                public static Task<List<T>> ToListAsync<T>(this IQueryable<T> q, CancellationToken ct = default) => Task.FromResult(new List<T>());
            }
            class OrderDto { }
            class Db { public DbSet<OrderDto> Orders { get; } = new(); }
            class MyHandler : IQueryHandler<GetOrdersQuery, List<OrderDto>> {
                private Db _db = new();
                public async Task<List<OrderDto>> Handle(GetOrdersQuery query, CancellationToken ct) {
                    return await _db.Orders.AsNoTracking().ToListAsync(ct);
                }
            }
            """;
        await Verify<QueryHandlerNoTrackingAnalyzer>.AnalyzerAsync(source);
    }
}

