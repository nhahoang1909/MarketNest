// tests/MarketNest.Analyzers.Tests/Naming/BannedClassSuffixAnalyzerTests.cs
using MarketNest.Analyzers.Naming;
using Xunit;

namespace MarketNest.Analyzers.Tests.Naming;

public class BannedClassSuffixAnalyzerTests
{
    [Theory]
    [InlineData("OrderManager")]
    [InlineData("CartHelper")]
    [InlineData("StringUtils")]
    public async Task Triggers_for_banned_suffix(string className)
    {
        var source = $$"""
            class {|MN002:{{className}}|} { }
            """;
        await Verify<BannedClassSuffixAnalyzer>.AnalyzerAsync(source);
    }

    [Theory]
    [InlineData("OrderRepository")]
    [InlineData("PlaceOrderCommand")]
    [InlineData("GetOrderDetailQuery")]
    public async Task No_trigger_for_valid_class_name(string className)
    {
        var source = $$"""
            class {{className}} { }
            """;
        await Verify<BannedClassSuffixAnalyzer>.AnalyzerAsync(source);
    }
}
