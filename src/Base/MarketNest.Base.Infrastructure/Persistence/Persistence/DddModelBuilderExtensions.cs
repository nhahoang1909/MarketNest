using MarketNest.Base.Domain;
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

    /// <summary>
    ///     Configures EF Core concurrency token for all entities implementing <see cref="IConcurrencyAware"/>.
    ///     This adds a WHERE clause on UPDATE/DELETE with the original <c>UpdateToken</c> value,
    ///     causing <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/> if another
    ///     transaction modified the row between read and write.
    /// </summary>
    /// <remarks>
    ///     Call this in each module's <c>OnModelCreating</c> after <see cref="ApplyDddPropertyAccessConventions"/>.
    ///     The <c>UpdateTokenInterceptor</c> rotates the token automatically on every save.
    /// </remarks>
    public static ModelBuilder ApplyConcurrencyTokenConventions(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(IConcurrencyAware).IsAssignableFrom(entityType.ClrType))
                continue;

            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(IConcurrencyAware.UpdateToken))
                .IsConcurrencyToken();
        }

        return modelBuilder;
    }
}

