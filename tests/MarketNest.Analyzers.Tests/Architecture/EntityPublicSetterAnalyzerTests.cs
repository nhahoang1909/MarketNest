// tests/MarketNest.Analyzers.Tests/Architecture/EntityPublicSetterAnalyzerTests.cs
using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class EntityPublicSetterAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_public_setter_on_Entity_subclass()
    {
        var source = """
            abstract class Entity<T> { }
            class Order : Entity<int> {
                public string {|MN016:Status|} { get; set; } = "";
            }
            """;
        await Verify<EntityPublicSetterAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_for_public_setter_on_AggregateRoot_subclass()
    {
        var source = """
            abstract class AggregateRoot { }
            class Order : AggregateRoot {
                public decimal {|MN016:Total|} { get; set; }
            }
            """;
        await Verify<EntityPublicSetterAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_private_setter()
    {
        var source = """
            abstract class Entity<T> { }
            class Order : Entity<int> {
                public string Status { get; private set; } = "";
            }
            """;
        await Verify<EntityPublicSetterAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_non_entity_class()
    {
        var source = """
            class OrderDto {
                public string Status { get; set; } = "";
            }
            """;
        await Verify<EntityPublicSetterAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_entity_implementing_ISoftDeletable()
    {
        var source = """
            interface ISoftDeletable { bool IsDeleted { get; set; } }
            abstract class Entity<T> { }
            class Order : Entity<int>, ISoftDeletable {
                public bool IsDeleted { get; set; }
            }
            """;
        await Verify<EntityPublicSetterAnalyzer>.AnalyzerAsync(source);
    }
}
