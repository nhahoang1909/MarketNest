// tests/MarketNest.Analyzers.Tests/Naming/CommandQueryNamingAnalyzerTests.cs
using MarketNest.Analyzers.Naming;
using Xunit;

namespace MarketNest.Analyzers.Tests.Naming;

public class CommandQueryNamingAnalyzerTests
{
    [Fact]
    public async Task MN012_triggers_when_ICommand_class_lacks_Command_suffix()
    {
        var source = """
            interface ICommand<T> { }
            class {|MN012:PlaceOrder|} : ICommand<string> { }
            """;
        await Verify<CommandQueryNamingAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task MN012_no_trigger_when_ICommand_class_has_Command_suffix()
    {
        var source = """
            interface ICommand<T> { }
            class PlaceOrderCommand : ICommand<string> { }
            """;
        await Verify<CommandQueryNamingAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task MN013_triggers_when_IQuery_class_lacks_Query_suffix()
    {
        var source = """
            interface IQuery<T> { }
            class {|MN013:GetOrderDetail|} : IQuery<string> { }
            """;
        await Verify<CommandQueryNamingAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task MN013_no_trigger_when_IQuery_class_has_correct_naming()
    {
        var source = """
            interface IQuery<T> { }
            class GetOrderDetailQuery : IQuery<string> { }
            """;
        await Verify<CommandQueryNamingAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task MN014_triggers_when_ICommandHandler_lacks_Handler_suffix()
    {
        var source = """
            using System.Threading.Tasks;
            interface ICommandHandler<TCommand, TResult> { Task Handle(TCommand c); }
            class {|MN014:PlaceOrderProcessor|} : ICommandHandler<string, int> {
                public Task Handle(string c) => Task.CompletedTask;
            }
            """;
        await Verify<CommandQueryNamingAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task MN015_triggers_when_IDomainEvent_record_lacks_Event_suffix()
    {
        var source = """
            interface IDomainEvent { }
            record {|MN015:OrderPlaced|} : IDomainEvent;
            """;
        await Verify<CommandQueryNamingAnalyzer>.AnalyzerAsync(source);
    }
}
