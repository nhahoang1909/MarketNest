// tests/MarketNest.Analyzers.Tests/AsyncRules/TaskFromResultAnalyzerTests.cs
using MarketNest.Analyzers.AsyncRules;
using MarketNest.Analyzers.CodeFixes;
using Xunit;

namespace MarketNest.Analyzers.Tests.AsyncRules;

public class TaskFromResultAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_await_Task_FromResult()
    {
        var source = """
            using System.Threading.Tasks;
            class C {
                async Task<int> M() => {|MN017:await Task.FromResult(42)|};
            }
            """;
        await Verify<TaskFromResultAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_await_on_real_async()
    {
        var source = """
            using System.Threading.Tasks;
            class C {
                async Task<int> M() => await Task.Delay(1).ContinueWith(_ => 42);
            }
            """;
        await Verify<TaskFromResultAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task CodeFix_removes_await_and_unwraps_value()
    {
        var source = """
            using System.Threading.Tasks;
            class C {
                async Task<int> M() => {|MN017:await Task.FromResult(42)|};
            }
            """;
        var fixedSource = """
            using System.Threading.Tasks;
            class C {
                async Task<int> M() => 42;
            }
            """;
        await VerifyFix<TaskFromResultAnalyzer, TaskFromResultCodeFix>.CodeFixAsync(source, fixedSource);
    }
}
