using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class EntityInitAccessorAnalyzerTests
{
    private const string InitPolyfill = """
        namespace System.Runtime.CompilerServices {
            internal static class IsExternalInit { }
        }
        """;

    [Fact]
    public async Task Triggers_for_init_accessor_on_Entity_subclass()
    {
        var source = InitPolyfill + """
            abstract class Entity<T> { }
            class Order : Entity<int> {
                public string {|MN028:Name|} { get; init; } = "";
            }
            """;
        await Verify<EntityInitAccessorAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_for_init_accessor_on_AggregateRoot_subclass()
    {
        var source = InitPolyfill + """
            abstract class AggregateRoot { }
            class Product : AggregateRoot {
                public decimal {|MN028:Price|} { get; init; }
            }
            """;
        await Verify<EntityInitAccessorAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_private_set()
    {
        var source = """
            abstract class Entity<T> { }
            class Order : Entity<int> {
                public string Name { get; private set; } = "";
            }
            """;
        await Verify<EntityInitAccessorAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_record_dto()
    {
        var source = InitPolyfill + """
            record OrderDto {
                public string Name { get; init; } = "";
            }
            """;
        await Verify<EntityInitAccessorAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_non_entity_class()
    {
        var source = InitPolyfill + """
            class OrderDto {
                public string Name { get; init; } = "";
            }
            """;
        await Verify<EntityInitAccessorAnalyzer>.AnalyzerAsync(source);
    }
}
