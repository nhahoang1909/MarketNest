using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class HandlerSaveChangesAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_SaveChangesAsync_in_command_handler()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            interface ICommandHandler<TCommand, TResult> {
                Task<TResult> Handle(TCommand cmd, CancellationToken ct);
            }
            record MyCommand();
            class MyDbContext { public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0); }
            class MyHandler : ICommandHandler<MyCommand, int> {
                private MyDbContext _db = new();
                public async Task<int> Handle(MyCommand cmd, CancellationToken ct) {
                    await {|MN024:_db.SaveChangesAsync(ct)|};
                    return 0;
                }
            }
            """;
        await Verify<HandlerSaveChangesAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_for_CommitAsync_in_command_handler()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            interface ICommandHandler<TCommand, TResult> {
                Task<TResult> Handle(TCommand cmd, CancellationToken ct);
            }
            record MyCommand();
            class UnitOfWork { public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask; }
            class MyHandler : ICommandHandler<MyCommand, int> {
                private UnitOfWork _uow = new();
                public async Task<int> Handle(MyCommand cmd, CancellationToken ct) {
                    await {|MN024:_uow.CommitAsync(ct)|};
                    return 0;
                }
            }
            """;
        await Verify<HandlerSaveChangesAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_non_handler_class()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            class MyDbContext { public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0); }
            class RegularClass {
                private MyDbContext _db = new();
                public async Task DoWork() {
                    await _db.SaveChangesAsync();
                }
            }
            """;
        await Verify<HandlerSaveChangesAnalyzer>.AnalyzerAsync(source);
    }
}

