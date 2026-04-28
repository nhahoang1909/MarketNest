// tests/MarketNest.Analyzers.Tests/AsyncRules/AsyncVoidAnalyzerTests.cs
using MarketNest.Analyzers.AsyncRules;
using MarketNest.Analyzers.CodeFixes;
using Xunit;

namespace MarketNest.Analyzers.Tests.AsyncRules;

public class AsyncVoidAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_async_void_method()
    {
        var source = """
            using System.Threading.Tasks;
            class C {
                public async void {|MN003:HandleOrder|}() { await Task.Delay(1); }
            }
            """;
        await Verify<AsyncVoidAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_async_task()
    {
        var source = """
            using System.Threading.Tasks;
            class C {
                public async Task HandleOrder() { await Task.Delay(1); }
            }
            """;
        await Verify<AsyncVoidAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_event_handler_with_EventArgs()
    {
        var source = """
            using System;
            using System.Threading.Tasks;
            class C {
                public async void OnClick(object sender, EventArgs e) { await Task.Delay(1); }
            }
            """;
        await Verify<AsyncVoidAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task CodeFix_changes_void_to_Task()
    {
        var source = """
            using System.Threading.Tasks;
            class C {
                public async void {|MN003:HandleOrder|}() { await Task.Delay(1); }
            }
            """;
        var fixedSource = """
            using System.Threading.Tasks;
            class C {
                public async Task HandleOrder() { await Task.Delay(1); }
            }
            """;
        await VerifyFix<AsyncVoidAnalyzer, AsyncVoidCodeFix>.CodeFixAsync(source, fixedSource);
    }

    [Fact]
    public async Task CodeFix_adds_using_when_missing()
    {
        var source = """
            class C {
                public async void {|MN003:HandleOrder|}() { }
            }
            """;
        var fixedSource = "using System.Threading.Tasks;\r\n\r\nclass C {\n    public async Task HandleOrder() { }\n}";
        await VerifyFix<AsyncVoidAnalyzer, AsyncVoidCodeFix>.CodeFixAsync(source, fixedSource);
    }
}
