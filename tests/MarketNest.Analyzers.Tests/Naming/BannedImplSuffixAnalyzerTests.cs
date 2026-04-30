using MarketNest.Analyzers.Naming;
using Xunit;

namespace MarketNest.Analyzers.Tests.Naming;

public class BannedImplSuffixAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_class_ending_with_Impl_suffix()
    {
        var source = """
            class {|MN022:OrderRepositoryImpl|} { }
            """;
        await Verify<BannedImplSuffixAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_normal_class_name()
    {
        var source = """
            class SqlOrderRepository { }
            """;
        await Verify<BannedImplSuffixAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_class_containing_Impl_in_middle()
    {
        var source = """
            class ImplicitConverter { }
            """;
        await Verify<BannedImplSuffixAnalyzer>.AnalyzerAsync(source);
    }
}

