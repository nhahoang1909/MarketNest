using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace MarketNest.ArchitectureTests;

/// <summary>
///     Enforces CQRS handler rules from code-rules.md §4.1 and backend-patterns.md §2:
///     - Command handlers must implement ICommandHandler&lt;,&gt;
///     - Query handlers must implement IQueryHandler&lt;,&gt;
///     - Commands must implement ICommand&lt;T&gt;
///     - Queries must implement IQuery&lt;T&gt;
///     - Handlers must reside in Application or Infrastructure namespace
/// </summary>
public class CqrsPatternTests
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
    // 1. Classes ending with "Command" must implement ICommand<T>
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(GetModuleAssemblies))]
    public void CommandClasses_ShouldImplementICommand(Assembly moduleAssembly)
    {
        var commandTypes = Types.InAssembly(moduleAssembly)
            .That()
            .HaveNameEndingWith("Command")
            .And()
            .AreNotInterfaces()
            .GetTypes()
            .Where(t => !t.Name.EndsWith("CommandHandler", StringComparison.Ordinal))
            .Where(t => !t.Name.EndsWith("CommandValidator", StringComparison.Ordinal))
            .ToList();

        foreach (var cmdType in commandTypes)
        {
            var implementsICommand = cmdType.GetInterfaces()
                .Any(i => i.IsGenericType &&
                          (i.GetGenericTypeDefinition().Name == "ICommand`1" ||
                           i.Name == "ICommand"));

            implementsICommand.Should().BeTrue(
                because: $"Class '{cmdType.FullName}' ends with 'Command' but does not implement ICommand<T>. " +
                         $"All commands must implement the CQRS contract (backend-patterns.md §2).");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. Classes ending with "Query" must implement IQuery<T>
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(GetModuleAssemblies))]
    public void QueryClasses_ShouldImplementIQuery(Assembly moduleAssembly)
    {
        // Only check concrete query RECORD types in the Application namespace,
        // not infrastructure BaseQuery implementations (e.g., AnnouncementQuery)
        var queryTypes = Types.InAssembly(moduleAssembly)
            .That()
            .HaveNameEndingWith("Query")
            .And()
            .AreNotInterfaces()
            .And()
            .ResideInNamespaceContaining(".Application")
            .GetTypes()
            .Where(t => !t.Name.EndsWith("QueryHandler", StringComparison.Ordinal))
            .Where(t => !t.Name.EndsWith("BaseQuery", StringComparison.Ordinal))
            .Where(t => !t.IsAbstract)
            .ToList();

        foreach (var queryType in queryTypes)
        {
            var implementsIQuery = queryType.GetInterfaces()
                .Any(i => i.IsGenericType &&
                          i.GetGenericTypeDefinition().Name == "IQuery`1");

            implementsIQuery.Should().BeTrue(
                because: $"Class '{queryType.FullName}' ends with 'Query' but does not implement IQuery<T>. " +
                         $"All queries must implement the CQRS contract (backend-patterns.md §2).");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. Command handlers must reside in Application namespace
    //    (code-rules.md §2.7: flat layer-level namespaces)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(GetModuleAssemblies))]
    public void CommandHandlers_ShouldResideInApplicationNamespace(Assembly moduleAssembly)
    {
        var handlerTypes = Types.InAssembly(moduleAssembly)
            .That()
            .ImplementInterface(typeof(MarketNest.Base.Common.ICommandHandler<,>))
            .GetTypes();

        foreach (var handler in handlerTypes)
        {
            var ns = handler.Namespace ?? "";
            ns.Should().EndWith(".Application",
                because: $"CommandHandler '{handler.Name}' must reside in the Application namespace, " +
                         $"not '{ns}'. Handlers belong in the Application layer (code-rules.md §4.1).");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. Query handlers must reside in Application or Infrastructure
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(GetModuleAssemblies))]
    public void QueryHandlers_ShouldResideInApplicationOrInfrastructureNamespace(Assembly moduleAssembly)
    {
        var handlerTypes = Types.InAssembly(moduleAssembly)
            .That()
            .ImplementInterface(typeof(MarketNest.Base.Common.IQueryHandler<,>))
            .GetTypes();

        foreach (var handler in handlerTypes)
        {
            var ns = handler.Namespace ?? "";
            var isValid = ns.EndsWith(".Application", StringComparison.Ordinal) || ns.EndsWith(".Infrastructure", StringComparison.Ordinal);
            isValid.Should().BeTrue(
                because: $"QueryHandler '{handler.Name}' must reside in Application or Infrastructure namespace, " +
                         $"not '{ns}'.");
        }
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
}
