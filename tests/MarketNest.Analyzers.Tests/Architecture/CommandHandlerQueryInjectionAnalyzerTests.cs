using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class CommandHandlerQueryInjectionAnalyzerTests
{
    // -------------------------------------------------------------------------
    // MN034 — triggers
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Triggers_when_command_handler_injects_IQuery_interface_primary_constructor()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            interface ICommandHandler<TCommand, TResult> {
                Task<TResult> Handle(TCommand cmd, CancellationToken ct);
            }
            record CreateOrderCommand();
            interface IGetOrdersQuery { }
            class CreateOrderHandler({|MN034:IGetOrdersQuery|} q) : ICommandHandler<CreateOrderCommand, int> {
                public Task<int> Handle(CreateOrderCommand cmd, CancellationToken ct) => Task.FromResult(0);
            }
            """;
        await Verify<CommandHandlerQueryInjectionAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_when_command_handler_injects_IQuery_via_explicit_constructor()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            interface ICommandHandler<TCommand, TResult> {
                Task<TResult> Handle(TCommand cmd, CancellationToken ct);
            }
            record PlaceOrderCommand();
            interface IOrderSummaryQuery { }
            class PlaceOrderHandler : ICommandHandler<PlaceOrderCommand, int> {
                public PlaceOrderHandler({|MN034:IOrderSummaryQuery|} query) { }
                public Task<int> Handle(PlaceOrderCommand cmd, CancellationToken ct) => Task.FromResult(0);
            }
            """;
        await Verify<CommandHandlerQueryInjectionAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_when_command_handler_injects_IQueryHandler_interface()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            interface ICommandHandler<TCommand, TResult> {
                Task<TResult> Handle(TCommand cmd, CancellationToken ct);
            }
            interface IQueryHandler<TQuery, TResult> {
                Task<TResult> Handle(TQuery query, CancellationToken ct);
            }
            record CreateOrderCommand();
            record GetOrderQuery();
            class CreateOrderHandler({|MN034:IQueryHandler<GetOrderQuery, int>|} qh)
                : ICommandHandler<CreateOrderCommand, int> {
                public Task<int> Handle(CreateOrderCommand cmd, CancellationToken ct) => Task.FromResult(0);
            }
            """;
        await Verify<CommandHandlerQueryInjectionAnalyzer>.AnalyzerAsync(source);
    }

    // -------------------------------------------------------------------------
    // MN034 — no triggers (valid usages)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task No_trigger_when_command_handler_injects_repository()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            interface ICommandHandler<TCommand, TResult> {
                Task<TResult> Handle(TCommand cmd, CancellationToken ct);
            }
            record PlaceOrderCommand();
            interface IOrderRepository { }
            class PlaceOrderHandler(IOrderRepository repo) : ICommandHandler<PlaceOrderCommand, int> {
                public Task<int> Handle(PlaceOrderCommand cmd, CancellationToken ct) => Task.FromResult(0);
            }
            """;
        await Verify<CommandHandlerQueryInjectionAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_non_handler_class()
    {
        var source = """
            interface IGetOrdersQuery { }
            class OrderProcessor(IGetOrdersQuery q) { }
            """;
        await Verify<CommandHandlerQueryInjectionAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_when_query_handler_injects_query_interface()
    {
        // MN034 only applies to CommandHandlers — should be silent for QueryHandlers
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            interface IQueryHandler<TQuery, TResult> {
                Task<TResult> Handle(TQuery query, CancellationToken ct);
            }
            record GetOrderQuery();
            interface IOrderItemQuery { }
            class GetOrderHandler(IOrderItemQuery itemQuery) : IQueryHandler<GetOrderQuery, int> {
                public Task<int> Handle(GetOrderQuery q, CancellationToken ct) => Task.FromResult(0);
            }
            """;
        await Verify<CommandHandlerQueryInjectionAnalyzer>.AnalyzerAsync(source);
    }
}

