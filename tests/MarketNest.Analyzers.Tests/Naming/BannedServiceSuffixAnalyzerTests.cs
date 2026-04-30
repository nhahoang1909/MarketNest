using MarketNest.Analyzers.Naming;
using Xunit;

namespace MarketNest.Analyzers.Tests.Naming;

public class BannedServiceSuffixAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_concrete_class_with_Service_suffix_not_implementing_interface()
    {
        var source = """
            class {|MN021:OrderService|} { }
            """;
        await Verify<BannedServiceSuffixAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_when_implementing_matching_interface()
    {
        var source = """
            interface INotificationService { }
            class NotificationService : INotificationService { }
            """;
        await Verify<BannedServiceSuffixAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_abstract_class()
    {
        var source = """
            abstract class BaseService { }
            """;
        await Verify<BannedServiceSuffixAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_class_without_Service_suffix()
    {
        var source = """
            class OrderProcessor { }
            """;
        await Verify<BannedServiceSuffixAnalyzer>.AnalyzerAsync(source);
    }
}

