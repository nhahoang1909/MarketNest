// tests/MarketNest.Analyzers.Tests/Architecture/ServiceLocatorAnalyzerTests.cs
using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class ServiceLocatorAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_GetRequiredService_in_CommandHandler()
    {
        var source = """
            using System;
            using System.Threading.Tasks;
            interface ICommandHandler<T, R> { Task Handle(T t); }
            interface IServiceProvider { object? GetService(Type t); }
            static class ServiceProviderExtensions {
                public static T GetRequiredService<T>(this IServiceProvider sp) => default!;
            }
            class PlaceOrderCommandHandler(IServiceProvider _sp) : ICommandHandler<string, int> {
                public Task Handle(string t) {
                    var svc = {|MN010:_sp.GetRequiredService<string>()|};
                    return Task.CompletedTask;
                }
            }
            """;
        await Verify<ServiceLocatorAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_outside_handler()
    {
        var source = """
            using System;
            interface IServiceProvider { object? GetService(Type t); }
            static class Ext { public static T GetRequiredService<T>(this IServiceProvider sp) => default!; }
            class Startup(IServiceProvider _sp) {
                void Configure() { var s = _sp.GetRequiredService<string>(); }
            }
            """;
        await Verify<ServiceLocatorAnalyzer>.AnalyzerAsync(source);
    }
}
