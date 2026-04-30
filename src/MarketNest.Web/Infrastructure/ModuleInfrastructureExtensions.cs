using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Scans module assemblies and registers all concrete implementations of
///     <see cref="IBaseRepository{TEntity,TKey}" /> and <see cref="IBaseQuery{TEntity,TKey}" />
///     with their declared service interfaces as <c>Scoped</c> services.
/// </summary>
/// <remarks>
///     How it works:
///     <list type="number">
///         <item>Iterates every concrete, non-generic class in the provided assemblies.</item>
///         <item>
///             Checks whether the class directly or transitively implements
///             <see cref="IBaseRepository{TEntity,TKey}" /> or <see cref="IBaseQuery{TEntity,TKey}" />.
///         </item>
///         <item>
///             For matched types, collects every non-system interface the class implements and
///             calls <c>TryAddScoped(serviceType, implementationType)</c> for each one —
///             so explicit registrations that appear first in <c>Program.cs</c> always win.
///         </item>
///     </list>
///     MediatR handlers (<c>ICommandHandler</c>, <c>IQueryHandler</c>) are already picked up by
///     <c>AddMediatR(cfg.RegisterServicesFromAssemblies(...))</c> and do NOT need to be listed here.
/// </remarks>
public static class ModuleInfrastructureExtensions
{
    private static readonly Type RepositoryMarker = typeof(IBaseRepository<,>);
    private static readonly Type QueryMarker = typeof(IBaseQuery<,>);

    /// <summary>
    ///     Auto-registers all repository and read-query implementations found in
    ///     <paramref name="assemblies" />.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="assemblies">Module assemblies to scan (use <c>AssemblyReference.Assembly</c>).</param>
    public static IServiceCollection AddModuleInfrastructureServices(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        foreach (Assembly assembly in assemblies)
        {
            IEnumerable<Type> concreteTypes = assembly
                .GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false });

            foreach (Type type in concreteTypes)
            {
                if (!ImplementsBaseMarker(type)) continue;

                IEnumerable<Type> serviceInterfaces = GetServiceInterfaces(type);

                foreach (Type iface in serviceInterfaces)
                    services.TryAddScoped(iface, type);
            }
        }

        return services;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool ImplementsBaseMarker(Type type) =>
        type.GetInterfaces().Any(IsBaseMarker);

    private static bool IsBaseMarker(Type i) =>
        i.IsGenericType &&
        (i.GetGenericTypeDefinition() == RepositoryMarker ||
         i.GetGenericTypeDefinition() == QueryMarker);

    /// <summary>
    ///     Returns all non-system interfaces the type declares, excluding raw open generics.
    ///     Both the concrete closed-generic base (e.g. <c>IBaseRepository&lt;Order,Guid&gt;</c>)
    ///     and the specific named interface (e.g. <c>IOrderRepository</c>) are included so DI
    ///     callers can inject either form.
    /// </summary>
    private static IEnumerable<Type> GetServiceInterfaces(Type type) =>
        type.GetInterfaces()
            .Where(i =>
                !i.IsGenericTypeDefinition &&
                i.Namespace is not null &&
                !i.Namespace.StartsWith("System", StringComparison.Ordinal) &&
                !i.Namespace.StartsWith("Microsoft", StringComparison.Ordinal));
}

