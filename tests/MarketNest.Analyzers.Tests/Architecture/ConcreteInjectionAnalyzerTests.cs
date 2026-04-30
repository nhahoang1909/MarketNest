using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class ConcreteInjectionAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_concrete_class_in_handler_primary_constructor()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            interface ICommandHandler<TCommand, TResult> {
                Task<TResult> Handle(TCommand cmd, CancellationToken ct);
            }
            record MyCommand();
            class OrderRepository { }
            class MyHandler({|MN030:OrderRepository|} repo) : ICommandHandler<MyCommand, int> {
                public Task<int> Handle(MyCommand cmd, CancellationToken ct) => Task.FromResult(0);
            }
            """;
        await Verify<ConcreteInjectionAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_interface_parameter()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            interface ICommandHandler<TCommand, TResult> {
                Task<TResult> Handle(TCommand cmd, CancellationToken ct);
            }
            record MyCommand();
            interface IOrderRepository { }
            class MyHandler(IOrderRepository repo) : ICommandHandler<MyCommand, int> {
                public Task<int> Handle(MyCommand cmd, CancellationToken ct) => Task.FromResult(0);
            }
            """;
        await Verify<ConcreteInjectionAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_non_handler_class()
    {
        var source = """
            class OrderRepository { }
            class RegularClass(OrderRepository repo) { }
            """;
        await Verify<ConcreteInjectionAnalyzer>.AnalyzerAsync(source);
    }
}

