// tests/MarketNest.Analyzers.Tests/Architecture/HandlerEntityReturnAnalyzerTests.cs
using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class HandlerEntityReturnAnalyzerTests
{
    // ─── trigger tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task Triggers_when_QueryHandler_returns_entity_directly()
    {
        var source = """
            using System.Threading.Tasks;
            abstract class Entity<T> { }
            class Order : Entity<int> { }
            interface IQueryHandler<TQ, TR> { Task<TR> Handle(TQ q); }
            class GetOrderByIdQuery { }
            class {|MN019:GetOrderByIdQueryHandler|} : IQueryHandler<GetOrderByIdQuery, Order>
            {
                public Task<Order> Handle(GetOrderByIdQuery q) => default!;
            }
            """;
        await Verify<HandlerEntityReturnAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_when_CommandHandler_returns_entity_directly()
    {
        var source = """
            using System.Threading.Tasks;
            abstract class Entity<T> { }
            class Order : Entity<int> { }
            interface ICommandHandler<TC, TR> { Task<TR> Handle(TC c); }
            class CreateOrderCommand { }
            class {|MN019:CreateOrderCommandHandler|} : ICommandHandler<CreateOrderCommand, Order>
            {
                public Task<Order> Handle(CreateOrderCommand c) => default!;
            }
            """;
        await Verify<HandlerEntityReturnAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_when_QueryHandler_returns_entity_wrapped_in_generic_result()
    {
        var source = """
            using System.Threading.Tasks;
            abstract class Entity<T> { }
            class Order : Entity<int> { }
            class Error { }
            class Result<T, E> { }
            interface IQueryHandler<TQ, TR> { Task<TR> Handle(TQ q); }
            class GetOrderByIdQuery { }
            class {|MN019:GetOrderByIdQueryHandler|} : IQueryHandler<GetOrderByIdQuery, Result<Order, Error>>
            {
                public Task<Result<Order, Error>> Handle(GetOrderByIdQuery q) => default!;
            }
            """;
        await Verify<HandlerEntityReturnAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_when_QueryHandler_returns_entity_in_list()
    {
        var source = """
            using System.Collections.Generic;
            using System.Threading.Tasks;
            abstract class Entity<T> { }
            class Order : Entity<int> { }
            interface IQueryHandler<TQ, TR> { Task<TR> Handle(TQ q); }
            class ListOrdersQuery { }
            class {|MN019:ListOrdersQueryHandler|} : IQueryHandler<ListOrdersQuery, IReadOnlyList<Order>>
            {
                public Task<IReadOnlyList<Order>> Handle(ListOrdersQuery q) => default!;
            }
            """;
        await Verify<HandlerEntityReturnAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_when_QueryHandler_returns_AggregateRoot_directly()
    {
        var source = """
            using System.Threading.Tasks;
            abstract class AggregateRoot { }
            class Cart : AggregateRoot { }
            interface IQueryHandler<TQ, TR> { Task<TR> Handle(TQ q); }
            class GetCartQuery { }
            class {|MN019:GetCartQueryHandler|} : IQueryHandler<GetCartQuery, Cart>
            {
                public Task<Cart> Handle(GetCartQuery q) => default!;
            }
            """;
        await Verify<HandlerEntityReturnAnalyzer>.AnalyzerAsync(source);
    }

    // ─── no-trigger tests ───────────────────────────────────────────────────

    [Fact]
    public async Task No_trigger_when_QueryHandler_returns_DTO()
    {
        var source = """
            using System.Threading.Tasks;
            class OrderDto { public int Id { get; set; } }
            interface IQueryHandler<TQ, TR> { Task<TR> Handle(TQ q); }
            class GetOrderByIdQuery { }
            class GetOrderByIdQueryHandler : IQueryHandler<GetOrderByIdQuery, OrderDto>
            {
                public Task<OrderDto> Handle(GetOrderByIdQuery q) => default!;
            }
            """;
        await Verify<HandlerEntityReturnAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_when_QueryHandler_returns_DTO_list()
    {
        var source = """
            using System.Collections.Generic;
            using System.Threading.Tasks;
            class OrderDto { public int Id { get; set; } }
            interface IQueryHandler<TQ, TR> { Task<TR> Handle(TQ q); }
            class ListOrdersQuery { }
            class ListOrdersQueryHandler : IQueryHandler<ListOrdersQuery, IReadOnlyList<OrderDto>>
            {
                public Task<IReadOnlyList<OrderDto>> Handle(ListOrdersQuery q) => default!;
            }
            """;
        await Verify<HandlerEntityReturnAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_non_handler_class_returning_entity()
    {
        var source = """
            using System.Threading.Tasks;
            abstract class Entity<T> { }
            class Order : Entity<int> { }
            class OrderService
            {
                public Task<Order> GetAsync(int id) => default!;
            }
            """;
        await Verify<HandlerEntityReturnAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_when_CommandHandler_returns_unit()
    {
        var source = """
            using System.Threading.Tasks;
            struct Unit { }
            interface ICommandHandler<TC, TR> { Task<TR> Handle(TC c); }
            class DeleteOrderCommand { }
            class DeleteOrderCommandHandler : ICommandHandler<DeleteOrderCommand, Unit>
            {
                public Task<Unit> Handle(DeleteOrderCommand c) => default!;
            }
            """;
        await Verify<HandlerEntityReturnAnalyzer>.AnalyzerAsync(source);
    }
}
