using MarketNest.Analyzers.CodeFixes;
using MarketNest.Analyzers.Logging;
using Xunit;

namespace MarketNest.Analyzers.Tests.Logging;

public class LoggingClassPartialAnalyzerTests
{
    [Fact]
    public async Task Triggers_when_class_has_IAppLogger_but_is_not_partial()
    {
        var source = """
            interface IAppLogger<T> { }
            class {|MN006:OrderHandler|}(IAppLogger<OrderHandler> _logger) { }
            """;
        await Verify<LoggingClassPartialAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_when_class_is_already_partial()
    {
        var source = """
            interface IAppLogger<T> { }
            partial class OrderHandler(IAppLogger<OrderHandler> _logger) { }
            """;
        await Verify<LoggingClassPartialAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_when_class_has_no_logger()
    {
        var source = """
            class OrderHandler(string name) { }
            """;
        await Verify<LoggingClassPartialAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task CodeFix_adds_partial_keyword()
    {
        var source = """
            interface IAppLogger<T> { }
            class {|MN006:OrderHandler|}(IAppLogger<OrderHandler> _logger) { }
            """;
        var fixedSource = """
            interface IAppLogger<T> { }
            partial class OrderHandler(IAppLogger<OrderHandler> _logger) { }
            """;
        await VerifyFix<LoggingClassPartialAnalyzer, LoggingClassPartialCodeFix>
            .CodeFixAsync(source, fixedSource);
    }
}
