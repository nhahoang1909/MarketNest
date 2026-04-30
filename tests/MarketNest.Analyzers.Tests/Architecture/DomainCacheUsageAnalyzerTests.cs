using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class DomainCacheUsageAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_ICacheService_usage_in_Domain_namespace()
    {
        var source = """
            namespace MarketNest.Orders.Domain;
            interface ICacheService { }
            class OrderDomainService {
                private {|MN033:ICacheService|} _cache;
            }
            """;
        await Verify<DomainCacheUsageAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_for_CacheKeys_in_Domain_namespace()
    {
        var source = """
            namespace MarketNest.Orders.Domain;
            static class CacheKeys { public static string Key = "x"; }
            class OrderEntity {
                void DoSomething() {
                    var k = {|MN033:CacheKeys|}.Key;
                }
            }
            """;
        await Verify<DomainCacheUsageAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_in_Application_namespace()
    {
        var source = """
            namespace MarketNest.Orders.Application;
            interface ICacheService { }
            class OrderAppService {
                private ICacheService _cache;
            }
            """;
        await Verify<DomainCacheUsageAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_in_Infrastructure_namespace()
    {
        var source = """
            namespace MarketNest.Orders.Infrastructure;
            interface ICacheService { }
            class CacheAdapter {
                private ICacheService _svc;
            }
            """;
        await Verify<DomainCacheUsageAnalyzer>.AnalyzerAsync(source);
    }
}

