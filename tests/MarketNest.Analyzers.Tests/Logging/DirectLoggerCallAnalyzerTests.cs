using MarketNest.Analyzers.Logging;
using Xunit;

namespace MarketNest.Analyzers.Tests.Logging;

public class DirectLoggerCallAnalyzerTests
{
    // Stub ILogger in the source itself since the test framework doesn't include NuGet packages.
    // The namespace block is declared first so that `using` directives inside it are valid.

    [Theory]
    [InlineData("LogInformation")]
    [InlineData("LogWarning")]
    [InlineData("LogError")]
    [InlineData("LogDebug")]
    public async Task Triggers_for_direct_logger_extension_call(string methodName)
    {
        var source = $$"""
            namespace Microsoft.Extensions.Logging {
                public interface ILogger { }
                public interface ILogger<T> : ILogger { }
                public static class LoggerExtensions {
                    public static void LogInformation(this ILogger logger, string msg) { }
                    public static void LogWarning(this ILogger logger, string msg) { }
                    public static void LogError(this ILogger logger, string msg) { }
                    public static void LogDebug(this ILogger logger, string msg) { }
                    public static void LogTrace(this ILogger logger, string msg) { }
                    public static void LogCritical(this ILogger logger, string msg) { }
                }
            }
            namespace App {
                using Microsoft.Extensions.Logging;
                class C {
                    private readonly ILogger<C> _logger;
                    void M() { {|MN005:_logger.{{methodName}}("msg")|};  }
                }
            }
            """;
        await Verify<DirectLoggerCallAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_Log_nested_class_call()
    {
        var source = """
            namespace Microsoft.Extensions.Logging {
                public interface ILogger { }
                public interface ILogger<T> : ILogger { }
            }
            namespace App {
                using Microsoft.Extensions.Logging;
                partial class C {
                    private readonly ILogger<C> _logger;
                    void M() { Log.InfoStart(_logger, "x"); }
                    private static partial class Log {
                        public static void InfoStart(ILogger logger, string s) { }
                    }
                }
            }
            """;
        await Verify<DirectLoggerCallAnalyzer>.AnalyzerAsync(source);
    }
}
