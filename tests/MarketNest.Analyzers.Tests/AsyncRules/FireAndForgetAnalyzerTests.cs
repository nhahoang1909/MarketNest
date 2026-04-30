using MarketNest.Analyzers.AsyncRules;
using Xunit;

namespace MarketNest.Analyzers.Tests.AsyncRules;

public class FireAndForgetAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_unawaited_task_returning_method()
    {
        var source = """
            using System.Threading.Tasks;
            class MyClass {
                Task DoWorkAsync() => Task.CompletedTask;
                void Run() {
                    {|MN023:DoWorkAsync()|};
                }
            }
            """;
        await Verify<FireAndForgetAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_when_awaited()
    {
        var source = """
            using System.Threading.Tasks;
            class MyClass {
                Task DoWorkAsync() => Task.CompletedTask;
                async Task Run() {
                    await DoWorkAsync();
                }
            }
            """;
        await Verify<FireAndForgetAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_when_assigned()
    {
        var source = """
            using System.Threading.Tasks;
            class MyClass {
                Task DoWorkAsync() => Task.CompletedTask;
                void Run() {
                    var t = DoWorkAsync();
                }
            }
            """;
        await Verify<FireAndForgetAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_void_method()
    {
        var source = """
            class MyClass {
                void DoWork() { }
                void Run() {
                    DoWork();
                }
            }
            """;
        await Verify<FireAndForgetAnalyzer>.AnalyzerAsync(source);
    }
}

