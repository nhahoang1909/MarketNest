using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class RepositoryIQueryableAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_IQueryable_return_type_in_repository_interface()
    {
        var source = """
            using System.Linq;
            interface IOrderRepository {
                {|MN027:IQueryable<string>|} GetAll();
            }
            """;
        await Verify<RepositoryIQueryableAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_IEnumerable_return_type()
    {
        var source = """
            using System.Collections.Generic;
            interface IOrderRepository {
                IEnumerable<string> GetAll();
            }
            """;
        await Verify<RepositoryIQueryableAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_non_repository_interface()
    {
        var source = """
            using System.Linq;
            interface IOrderQuery {
                IQueryable<string> GetAll();
            }
            """;
        await Verify<RepositoryIQueryableAnalyzer>.AnalyzerAsync(source);
    }
}

