using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class DeepIncludeChainAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_three_ThenInclude_levels()
    {
        var source = """
            using System.Linq;
            using System.Collections.Generic;
            class DbSet<T> { }
            static class Ext {
                public static DbSet<T> Include<T>(this DbSet<T> s, System.Func<T, object> f) => s;
                public static DbSet<T> ThenInclude<T>(this DbSet<T> s, System.Func<T, object> f) => s;
            }
            class Order { }
            class Db { public DbSet<Order> Orders { get; } = new(); }
            class MyClass {
                private Db _db = new();
                void Run() {
                    _db.Orders
                        .Include(o => o)
                        .ThenInclude(o => o)
                        .ThenInclude(o => o)
                        .{|MN032:ThenInclude|}(o => o);
                }
            }
            """;
        await Verify<DeepIncludeChainAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_two_ThenInclude_levels()
    {
        var source = """
            using System.Linq;
            class DbSet<T> { }
            static class Ext {
                public static DbSet<T> Include<T>(this DbSet<T> s, System.Func<T, object> f) => s;
                public static DbSet<T> ThenInclude<T>(this DbSet<T> s, System.Func<T, object> f) => s;
            }
            class Order { }
            class Db { public DbSet<Order> Orders { get; } = new(); }
            class MyClass {
                private Db _db = new();
                void Run() {
                    _db.Orders
                        .Include(o => o)
                        .ThenInclude(o => o)
                        .ThenInclude(o => o);
                }
            }
            """;
        await Verify<DeepIncludeChainAnalyzer>.AnalyzerAsync(source);
    }
}

