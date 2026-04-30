using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class QueryHandlerSaveChangesAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_SaveChangesAsync_in_query_handler()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            interface IQueryHandler<TQuery, TResult> {
                Task<TResult> Handle(TQuery query, CancellationToken ct);
            }
            record MyQuery();
            class Db { public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0); }
            class MyHandler : IQueryHandler<MyQuery, int> {
                private Db _db = new();
                public async Task<int> Handle(MyQuery query, CancellationToken ct) {
                    await {|MN031:_db.SaveChangesAsync(ct)|};
                    return 0;
                }
            }
            """;
        await Verify<QueryHandlerSaveChangesAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_non_query_handler()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            class Db { public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0); }
            class RegularClass {
                private Db _db = new();
                public async Task DoWork() { await _db.SaveChangesAsync(); }
            }
            """;
        await Verify<QueryHandlerSaveChangesAnalyzer>.AnalyzerAsync(source);
    }
}

