// tests/MarketNest.Analyzers.Tests/AsyncRules/BlockingAsyncAnalyzerTests.cs
using MarketNest.Analyzers.AsyncRules;
using Xunit;

namespace MarketNest.Analyzers.Tests.AsyncRules;

public class BlockingAsyncAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_dot_Result()
    {
        var source = """
            using System.Threading.Tasks;
            class C {
                void M() {
                    var t = Task.FromResult(1);
                    var x = {|MN004:t.Result|};
                }
            }
            """;
        await Verify<BlockingAsyncAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_for_GetAwaiter_GetResult()
    {
        var source = """
            using System.Threading.Tasks;
            class C {
                void M() {
                    var t = Task.FromResult(1);
                    var x = {|MN004:t.GetAwaiter|}().GetResult();
                }
            }
            """;
        await Verify<BlockingAsyncAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_await()
    {
        var source = """
            using System.Threading.Tasks;
            class C {
                async Task M() {
                    var x = await Task.FromResult(1);
                }
            }
            """;
        await Verify<BlockingAsyncAnalyzer>.AnalyzerAsync(source);
    }
}
