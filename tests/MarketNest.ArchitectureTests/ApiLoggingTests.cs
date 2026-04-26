using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace MarketNest.ArchitectureTests;

public class ApiLoggingTests
{
    // Use a well-known PageModel type from the Web project to resolve the assembly
    private static readonly Assembly WebAssembly = typeof(MarketNest.Web.Pages.IndexModel).Assembly;

    [Fact]
    public void PageModelsShouldInjectIAppLogger()
    {
        var result = Types.InAssembly(WebAssembly)
            .That()
            .ResideInNamespace("MarketNest.Web.Pages")
            .And()
            .AreClasses()
            .And()
            .HaveNameEndingWith("Model")
            .Should()
            .HaveDependencyOn("MarketNest.Base.Infrastructure.IAppLogger`1")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Razor PageModel classes that act as API entrypoints must inject IAppLogger<T> for traceable logging. " +
                     "Violations: " + (result.FailingTypeNames is null ? "(none)" : string.Join(", ", result.FailingTypeNames)));
    }

    [Fact]
    public void ControllersShouldInjectIAppLogger()
    {
        var result = Types.InAssembly(WebAssembly)
            .That()
            .ResideInNamespace("MarketNest.Web")
            .And()
            .AreClasses()
            .And()
            .HaveNameEndingWith("Controller")
            .Should()
            .HaveDependencyOn("MarketNest.Base.Infrastructure.IAppLogger`1")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "API Controllers must inject IAppLogger<T> for traceable logging. " +
                     "Violations: " + (result.FailingTypeNames is null ? "(none)" : string.Join(", ", result.FailingTypeNames)));
    }
}


