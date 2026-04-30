using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class DomainInfrastructureReferenceAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_EFCore_using_in_Domain_namespace()
    {
        var source = """
            {|MN026:using Microsoft.EntityFrameworkCore;|}
            namespace MarketNest.Orders.Domain;
            class Order { }
            """;
        await Verify<DomainInfrastructureReferenceAnalyzer>.AnalyzerIgnoringCompilerAsync(source);
    }

    [Fact]
    public async Task Triggers_for_Redis_using_in_Domain_namespace()
    {
        var source = """
            {|MN026:using StackExchange.Redis;|}
            namespace MarketNest.Catalog.Domain;
            class Product { }
            """;
        await Verify<DomainInfrastructureReferenceAnalyzer>.AnalyzerIgnoringCompilerAsync(source);
    }

    [Fact]
    public async Task No_trigger_in_Infrastructure_namespace()
    {
        var source = """
            using Microsoft.EntityFrameworkCore;
            namespace MarketNest.Orders.Infrastructure;
            class OrderDbContext { }
            """;
        await Verify<DomainInfrastructureReferenceAnalyzer>.AnalyzerIgnoringCompilerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_allowed_using_in_Domain()
    {
        var source = """
            using System;
            using System.Collections.Generic;
            namespace MarketNest.Orders.Domain;
            class Order { }
            """;
        await Verify<DomainInfrastructureReferenceAnalyzer>.AnalyzerAsync(source);
    }
}
