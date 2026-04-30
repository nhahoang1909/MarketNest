using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class HandlerTransactionAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_BeginTransactionAsync_in_command_handler()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            interface ICommandHandler<TCommand, TResult> {
                Task<TResult> Handle(TCommand cmd, CancellationToken ct);
            }
            record MyCommand();
            class MyDb { public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask; }
            class MyHandler : ICommandHandler<MyCommand, int> {
                private MyDb _db = new();
                public async Task<int> Handle(MyCommand cmd, CancellationToken ct) {
                    await {|MN025:_db.BeginTransactionAsync(ct)|};
                    return 0;
                }
            }
            """;
        await Verify<HandlerTransactionAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_for_BeginTransactionAsync_in_query_handler()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            interface IQueryHandler<TQuery, TResult> {
                Task<TResult> Handle(TQuery query, CancellationToken ct);
            }
            record MyQuery();
            class MyDb { public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask; }
            class MyHandler : IQueryHandler<MyQuery, int> {
                private MyDb _db = new();
                public async Task<int> Handle(MyQuery query, CancellationToken ct) {
                    await {|MN025:_db.BeginTransactionAsync(ct)|};
                    return 0;
                }
            }
            """;
        await Verify<HandlerTransactionAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_non_handler_class()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            class MyDb { public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask; }
            class BackgroundJob {
                private MyDb _db = new();
                public async Task Execute(CancellationToken ct) {
                    await _db.BeginTransactionAsync(ct);
                }
            }
            """;
        await Verify<HandlerTransactionAnalyzer>.AnalyzerAsync(source);
    }
}

