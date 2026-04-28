// tests/MarketNest.Analyzers.Tests/Architecture/DateTimeUsageAnalyzerTests.cs
using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class DateTimeUsageAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_DateTime_property()
    {
        var source = """
            using System;
            class Order {
                public {|MN009:DateTime|} CreatedAt { get; private set; }
            }
            """;
        await Verify<DateTimeUsageAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_for_nullable_DateTime_property()
    {
        var source = """
            using System;
            class Order {
                public {|MN009:DateTime?|} ShippedAt { get; private set; }
            }
            """;
        await Verify<DateTimeUsageAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_DateTimeOffset()
    {
        var source = """
            using System;
            class Order {
                public DateTimeOffset CreatedAt { get; private set; }
            }
            """;
        await Verify<DateTimeUsageAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_for_DateTime_field()
    {
        var source = """
            using System;
            class Foo {
                private {|MN009:DateTime|} _timestamp;
            }
            """;
        await Verify<DateTimeUsageAnalyzer>.AnalyzerAsync(source);
    }
}
