using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class QueryHandlerWriteInjectionAnalyzerTests
{
    // -------------------------------------------------------------------------
    // MN035 — triggers (write-side / handler injection)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Triggers_when_query_handler_injects_repository_primary_constructor()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            interface IQueryHandler<TQuery, TResult> {
                Task<TResult> Handle(TQuery query, CancellationToken ct);
            }
            record GetOrderQuery();
            interface IOrderRepository { }
            class GetOrderHandler({|MN035:IOrderRepository|} repo) : IQueryHandler<GetOrderQuery, int> {
                public Task<int> Handle(GetOrderQuery q, CancellationToken ct) => Task.FromResult(0);
            }
            """;
        await Verify<QueryHandlerWriteInjectionAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_when_query_handler_injects_repository_via_explicit_constructor()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            interface IQueryHandler<TQuery, TResult> {
                Task<TResult> Handle(TQuery query, CancellationToken ct);
            }
            record GetOrderQuery();
            interface ICartRepository { }
            class GetOrderHandler : IQueryHandler<GetOrderQuery, int> {
                public GetOrderHandler({|MN035:ICartRepository|} repo) { }
                public Task<int> Handle(GetOrderQuery q, CancellationToken ct) => Task.FromResult(0);
            }
            """;
        await Verify<QueryHandlerWriteInjectionAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_when_query_handler_injects_ICommandHandler_interface()
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
            record PlaceOrderCommand();
            record GetOrderQuery();
            class GetOrderHandler({|MN035:ICommandHandler<PlaceOrderCommand, int>|} ch)
                : IQueryHandler<GetOrderQuery, int> {
                public Task<int> Handle(GetOrderQuery q, CancellationToken ct) => Task.FromResult(0);
            }
            """;
        await Verify<QueryHandlerWriteInjectionAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_when_query_handler_injects_another_IQueryHandler()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            interface IQueryHandler<TQuery, TResult> {
                Task<TResult> Handle(TQuery query, CancellationToken ct);
            }
            record GetOrderQuery();
            record GetItemsQuery();
            class GetOrderHandler({|MN035:IQueryHandler<GetItemsQuery, int>|} inner)
                : IQueryHandler<GetOrderQuery, int> {
                public Task<int> Handle(GetOrderQuery q, CancellationToken ct) => Task.FromResult(0);
            }
            """;
        await Verify<QueryHandlerWriteInjectionAnalyzer>.AnalyzerAsync(source);
    }

    // -------------------------------------------------------------------------
    // MN035 — no triggers (valid usages)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task No_trigger_when_query_handler_injects_IQuery_interface()
    {
        // Injecting I*Query is the correct pattern for cross-aggregate reads
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
        await Verify<QueryHandlerWriteInjectionAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_command_handler_injecting_repository()
    {
        // MN035 only applies to QueryHandlers — should be silent for CommandHandlers
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
        await Verify<QueryHandlerWriteInjectionAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_non_handler_class()
    {
        var source = """
            interface IOrderRepository { }
            class ReportBuilder(IOrderRepository repo) { }
            """;
        await Verify<QueryHandlerWriteInjectionAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_when_query_handler_injects_logger_and_query_interface()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            interface IQueryHandler<TQuery, TResult> {
                Task<TResult> Handle(TQuery query, CancellationToken ct);
            }
            namespace Microsoft.Extensions.Logging {
                interface ILogger<T> { }
            }
            record GetOrderQuery();
            interface IOrderItemQuery { }
            class GetOrderHandler(
                IOrderItemQuery itemQuery,
                Microsoft.Extensions.Logging.ILogger<GetOrderHandler> logger)
                : IQueryHandler<GetOrderQuery, int> {
                public Task<int> Handle(GetOrderQuery q, CancellationToken ct) => Task.FromResult(0);
            }
            """;
        await Verify<QueryHandlerWriteInjectionAnalyzer>.AnalyzerAsync(source);
    }
}

