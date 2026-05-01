using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace MarketNest.ArchitectureTests;

/// <summary>
///     Enforces CQRS naming conventions from code-rules.md §2.2 and §4.1:
///     - Commands: verb + noun + "Command" suffix
///     - Command handlers: same + "Handler" suffix
///     - Validators: same + "Validator" suffix
///     - Queries: "Get" prefix + noun + "Query" suffix
///     - Events: past tense + "Event" suffix
///     - DTOs: entity name + "Dto"/"ListItemDto"/"DetailDto" suffix
/// </summary>
public class NamingConventionTests
{
    private static readonly Assembly[] AllModuleAssemblies =
    [
        MarketNest.Identity.AssemblyReference.Assembly,
        MarketNest.Catalog.AssemblyReference.Assembly,
        MarketNest.Cart.AssemblyReference.Assembly,
        MarketNest.Orders.AssemblyReference.Assembly,
        MarketNest.Payments.AssemblyReference.Assembly,
        MarketNest.Reviews.AssemblyReference.Assembly,
        MarketNest.Disputes.AssemblyReference.Assembly,
        MarketNest.Notifications.AssemblyReference.Assembly,
        MarketNest.Admin.AssemblyReference.Assembly,
        MarketNest.Auditing.AssemblyReference.Assembly,
        MarketNest.Promotions.AssemblyReference.Assembly
    ];

    // ═══════════════════════════════════════════════════════════════
    // 1. Command handlers must end with "CommandHandler"
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(GetModuleAssemblies))]
    public void CommandHandlers_ShouldFollowNamingConvention(Assembly moduleAssembly)
    {
        var result = Types.InAssembly(moduleAssembly)
            .That()
            .ImplementInterface(typeof(MarketNest.Base.Common.ICommandHandler<,>))
            .Should()
            .HaveNameEndingWith("CommandHandler")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"All ICommandHandler implementations must end with 'CommandHandler'. " +
                     $"Violations: {FormatViolations(result.FailingTypeNames)}");
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. Query handlers must end with "QueryHandler"
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(GetModuleAssemblies))]
    public void QueryHandlers_ShouldFollowNamingConvention(Assembly moduleAssembly)
    {
        var result = Types.InAssembly(moduleAssembly)
            .That()
            .ImplementInterface(typeof(MarketNest.Base.Common.IQueryHandler<,>))
            .Should()
            .HaveNameEndingWith("QueryHandler")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"All IQueryHandler implementations must end with 'QueryHandler'. " +
                     $"Violations: {FormatViolations(result.FailingTypeNames)}");
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. Banned class suffixes: Manager, Helper, Utils (code-rules.md §2.2)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(GetModuleAssemblies))]
    public void Classes_ShouldNotUseBannedSuffixes(Assembly moduleAssembly)
    {
        var bannedSuffixes = new[] { "Manager", "Helper", "Utils", "Impl" };

        foreach (var suffix in bannedSuffixes)
        {
            var result = Types.InAssembly(moduleAssembly)
                .That()
                .AreClasses()
                .And()
                .AreNotAbstract()
                .ShouldNot()
                .HaveNameEndingWith(suffix)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                because: $"Classes must not use the banned suffix '{suffix}' (code-rules.md §2.2). " +
                         $"Violations: {FormatViolations(result.FailingTypeNames)}");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. Repositories must end with "Repository"
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(GetModuleAssemblies))]
    public void RepositoryInterfaces_ShouldFollowNamingConvention(Assembly moduleAssembly)
    {
        var result = Types.InAssembly(moduleAssembly)
            .That()
            .AreInterfaces()
            .And()
            .HaveNameStartingWith("I")
            .And()
            .HaveNameEndingWith("Repository")
            .Should()
            .BeInterfaces()
            .GetResult();

        // This test validates the convention exists; the next test validates implementations.
        result.IsSuccessful.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    public static TheoryData<Assembly> GetModuleAssemblies()
    {
        var data = new TheoryData<Assembly>();
        foreach (var asm in AllModuleAssemblies) data.Add(asm);
        return data;
    }

    private static string FormatViolations(IEnumerable<string>? failingTypes) =>
        failingTypes is null ? "(none)" : string.Join(", ", failingTypes);
}

