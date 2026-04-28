using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MarketNest.Base.Infrastructure;

/// <summary>
///     EF Core model builder extensions for DDD entity conventions.
///     Ensures EF Core correctly materializes entities with <c>{ get; private set; }</c> properties
///     and collection navigations backed by private fields.
/// </summary>
/// <remarks>
///     <b>Why this is needed (ADR-007 + ADR-023):</b>
///     <list type="bullet">
///         <item>DDD entities use <c>{ get; private set; }</c> — EF Core handles scalar properties
///               via the compiler-generated backing field automatically.</item>
///         <item>Collection navigations (e.g. <c>IReadOnlyList&lt;T&gt;</c>) backed by
///               an explicit private field (e.g. <c>_items</c>) require <c>PropertyAccessMode.Field</c>
///               so EF Core populates the backing field rather than trying to call the property setter.</item>
///     </list>
///     Call <see cref="ApplyDddPropertyAccessConventions" /> in each module's <c>OnModelCreating</c>
///     to apply these conventions automatically.
/// </remarks>
public static class DddModelBuilderExtensions
{
    /// <summary>
    ///     Applies DDD-friendly property access conventions to all entities in the model:
    ///     <list type="number">
    ///         <item>Sets the model default to <c>PropertyAccessMode.PreferField</c> (explicit —
    ///               matches EF Core default but documents intent for DDD entities).</item>
    ///         <item>Detects collection navigations with an explicit private backing field
    ///               (naming convention: <c>_camelCase</c> for <c>PascalCase</c> property)
    ///               and sets <c>PropertyAccessMode.Field</c> on those navigations.</item>
    ///     </list>
    /// </summary>
    public static ModelBuilder ApplyDddPropertyAccessConventions(this ModelBuilder modelBuilder)
    {
        // 1. Model-level: prefer backing fields for all properties (explicit intent for DDD).
        //    EF Core default is already PreferField, but we make it explicit for clarity.
        modelBuilder.UsePropertyAccessMode(PropertyAccessMode.PreferField);

        // 2. For each entity, detect collection navigations with explicit backing fields
        //    and force PropertyAccessMode.Field so EF Core uses the _field directly.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            foreach (var navigation in entityType.GetNavigations())
            {
                if (!navigation.IsCollection)
                    continue;

                // Convention: backing field name is _camelCase of the PascalCase property name
                var propertyName = navigation.Name;
                var backingFieldName = $"_{char.ToLowerInvariant(propertyName[0])}{propertyName[1..]}";

                var fieldInfo = clrType.GetField(
                    backingFieldName,
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (fieldInfo is not null)
                {
                    navigation.SetPropertyAccessMode(PropertyAccessMode.Field);
                }
            }
        }

        return modelBuilder;
    }
}

