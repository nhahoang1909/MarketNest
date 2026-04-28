// tests/MarketNest.Analyzers.Tests/Architecture/FlatNamespaceAnalyzerTests.cs
using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class FlatNamespaceAnalyzerTests
{
    [Theory]
    [InlineData("MarketNest.Orders.Application.Commands")]
    [InlineData("MarketNest.Identity.Domain.Entities")]
    [InlineData("MarketNest.Admin.Infrastructure.Persistence")]
    public async Task Triggers_when_namespace_has_more_than_three_segments(string ns)
    {
        var source = $$"""
            {|MN008:namespace {{ns}};|}
            class C { }
            """;
        await Verify<FlatNamespaceAnalyzer>.AnalyzerAsync(source);
    }

    [Theory]
    [InlineData("MarketNest.Orders.Application")]
    [InlineData("MarketNest.Identity.Domain")]
    [InlineData("MarketNest.Admin")]
    public async Task No_trigger_for_three_or_fewer_segments(string ns)
    {
        var source = $$"""
            namespace {{ns}};
            class C { }
            """;
        await Verify<FlatNamespaceAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_non_marketnest_namespace()
    {
        var source = """
            namespace Some.Other.Deep.Namespace;
            class C { }
            """;
        await Verify<FlatNamespaceAnalyzer>.AnalyzerAsync(source);
    }
}
