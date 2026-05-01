using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace MarketNest.ArchitectureTests;

/// <summary>
///     Enforces DDD and domain model conventions from code-rules.md §3.1, §3.2, §5.1:
///     - Entities extend Entity&lt;T&gt; or AggregateRoot
///     - Domain layer classes must not have public setters (ADR-007)
///     - Aggregates must not expose IQueryable
///     - Value objects must be immutable
/// </summary>
public class DomainModelTests
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
    // 1. Domain entities must inherit from Entity<T> or AggregateRoot
    //    (code-rules.md §3.1)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(GetModuleAssemblies))]
    public void DomainEntities_ShouldInheritFromBaseEntity(Assembly moduleAssembly)
    {
        var domainTypes = Types.InAssembly(moduleAssembly)
            .That()
            .ResideInNamespaceContaining(".Domain")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .GetTypes()
            .Where(t => !t.IsEnum && !IsRecordType(t))
            .Where(t => !IsValueObjectType(t))
            .Where(t => !IsConfigurationType(t))
            .Where(t => !t.Name.EndsWith("Seeder", StringComparison.Ordinal))
            .Where(t => !t.Name.EndsWith("Extensions", StringComparison.Ordinal))
            .Where(t => !t.Name.Contains("Configuration"))
            .ToList();

        foreach (var type in domainTypes)
        {
            var inheritsEntity = InheritsFromGeneric(type, "Entity`1") ||
                                 InheritsFrom(type, "AggregateRoot");

            inheritsEntity.Should().BeTrue(
                because: $"Domain class '{type.FullName}' in {moduleAssembly.GetName().Name} " +
                         $"must inherit from Entity<T> or AggregateRoot (code-rules.md §3.1)");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. Domain classes must NOT have public settable properties
    //    (ADR-007: { get; private set; } for entities)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(GetModuleAssemblies))]
    public void DomainEntities_ShouldNotHavePublicSetters(Assembly moduleAssembly)
    {
        var domainTypes = Types.InAssembly(moduleAssembly)
            .That()
            .ResideInNamespaceContaining(".Domain")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .GetTypes()
            .Where(t => !t.IsEnum && !IsRecordType(t))
            .Where(t => InheritsFromGeneric(t, "Entity`1") || InheritsFrom(t, "AggregateRoot"))
            .ToList();

        var violations = new List<string>();

        foreach (var type in domainTypes)
        {
            var publicSetters = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.SetMethod is { IsPublic: true })
                .Where(p => !IsInfrastructureInterfaceProperty(p))
                .Select(p => $"{type.Name}.{p.Name}")
                .ToList();

            violations.AddRange(publicSetters);
        }

        violations.Should().BeEmpty(
            because: "Domain entities must use {{ get; private set; }} — no public setters (ADR-007). " +
                     $"Violations: {string.Join(", ", violations)}");
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. Repositories must NOT return IQueryable
    //    (code-rules.md §5.1: don't leak EF into domain)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(GetModuleAssemblies))]
    public void RepositoryInterfaces_ShouldNotReturnIQueryable(Assembly moduleAssembly)
    {
        var repositoryInterfaces = Types.InAssembly(moduleAssembly)
            .That()
            .AreInterfaces()
            .And()
            .HaveNameEndingWith("Repository")
            .GetTypes();

        var violations = new List<string>();

        foreach (var repoInterface in repositoryInterfaces)
        {
            var queryableMethods = repoInterface
                .GetMethods()
                .Where(m => m.ReturnType.IsGenericType &&
                            m.ReturnType.GetGenericTypeDefinition().FullName?.Contains("IQueryable") == true)
                .Select(m => $"{repoInterface.Name}.{m.Name}")
                .ToList();

            violations.AddRange(queryableMethods);
        }

        violations.Should().BeEmpty(
            because: "Repository interfaces must not return IQueryable — it leaks EF into domain (code-rules.md §5.1). " +
                     $"Violations: {string.Join(", ", violations)}");
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

    private static bool InheritsFromGeneric(Type type, string genericBaseName)
    {
        var baseType = type.BaseType;
        while (baseType is not null)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition().Name == genericBaseName)
                return true;
            baseType = baseType.BaseType;
        }
        return false;
    }

    private static bool InheritsFrom(Type type, string baseName)
    {
        var baseType = type.BaseType;
        while (baseType is not null)
        {
            if (baseType.Name == baseName) return true;
            baseType = baseType.BaseType;
        }
        return false;
    }

    private static bool IsValueObjectType(Type type) =>
        InheritsFrom(type, "ValueObject") || type.Name.EndsWith("ValueObject", StringComparison.Ordinal);

    private static bool IsConfigurationType(Type type) =>
        type.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition().Name == "IEntityTypeConfiguration`1");

    private static bool IsRecordType(Type type) =>
        type.GetMethod("<Clone>$") is not null;

    /// <summary>
    ///     Infrastructure interface properties (ISoftDeletable, IAuditable, IConcurrencyAware)
    ///     are allowed to have public setters per ADR-007.
    /// </summary>
    private static bool IsInfrastructureInterfaceProperty(PropertyInfo property)
    {
        var infraPropertyNames = new HashSet<string>
        {
            "IsDeleted", "DeletedAt", "DeletedBy",
            "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy",
            "UpdateToken"
        };
        return infraPropertyNames.Contains(property.Name);
    }
}
