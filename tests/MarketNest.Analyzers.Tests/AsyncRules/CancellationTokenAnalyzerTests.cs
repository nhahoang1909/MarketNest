// tests/MarketNest.Analyzers.Tests/AsyncRules/CancellationTokenAnalyzerTests.cs
using MarketNest.Analyzers.AsyncRules;
using Xunit;

namespace MarketNest.Analyzers.Tests.AsyncRules;

public class CancellationTokenAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_interface_async_method_missing_CT()
    {
        var source = """
            using System.Threading.Tasks;
            interface IOrderRepository {
                Task<string?> {|MN011:GetByIdAsync|}(int id);
            }
            """;
        await Verify<CancellationTokenAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_for_abstract_method_missing_CT()
    {
        var source = """
            using System.Threading.Tasks;
            abstract class Base {
                public abstract Task {|MN011:SaveAsync|}();
            }
            """;
        await Verify<CancellationTokenAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_when_CT_present()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            interface IOrderRepository {
                Task<string?> GetByIdAsync(int id, CancellationToken ct);
            }
            """;
        await Verify<CancellationTokenAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_concrete_implementation()
    {
        var source = """
            using System.Threading.Tasks;
            class Repo {
                public Task<string?> GetByIdAsync(int id) => Task.FromResult<string?>(null);
            }
            """;
        await Verify<CancellationTokenAnalyzer>.AnalyzerAsync(source);
    }
}
