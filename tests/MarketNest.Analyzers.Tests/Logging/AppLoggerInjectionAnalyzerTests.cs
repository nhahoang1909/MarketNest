using MarketNest.Analyzers.CodeFixes;
using MarketNest.Analyzers.Logging;
using Xunit;

namespace MarketNest.Analyzers.Tests.Logging;

public class AppLoggerInjectionAnalyzerTests
{
    private const string ILoggerStub = """
        namespace Microsoft.Extensions.Logging {
            public interface ILogger { }
            public interface ILogger<T> : ILogger { }
        }
        """;

    [Fact]
    public async Task Triggers_for_ILogger_in_primary_constructor()
    {
        var source = ILoggerStub + """
            namespace App {
                using Microsoft.Extensions.Logging;
                partial class Handler(
                    {|MN007:ILogger<Handler>|} _logger) { }
            }
            """;
        await Verify<AppLoggerInjectionAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_IAppLogger()
    {
        var source = """
            interface IAppLogger<T> { }
            partial class Handler(IAppLogger<Handler> _logger) { }
            """;
        await Verify<AppLoggerInjectionAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task CodeFix_replaces_ILogger_with_IAppLogger()
    {
        // Both stubs must be top-level namespaces so the file-level using directive can resolve them.
        const string appLoggerStub = """
            namespace MarketNest.Base.Infrastructure {
                public interface IAppLogger<T> { }
            }
            """;

        var source = ILoggerStub + appLoggerStub + """
            namespace App {
                using Microsoft.Extensions.Logging;
                partial class Handler(
                    {|MN007:ILogger<Handler>|} _logger) { }
            }
            """;
        // CarriageReturnLineFeed trailing trivia on the inserted using + source leading \n = blank line.
        var fixedSource = "using MarketNest.Base.Infrastructure;\r\n" + source
            .Replace("{|MN007:ILogger<Handler>|}", "IAppLogger<Handler>");
        await VerifyFix<AppLoggerInjectionAnalyzer, AppLoggerInjectionCodeFix>
            .CodeFixAsync(source, fixedSource);
    }
}
