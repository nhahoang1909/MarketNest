using MarketNest.Base.Domain;

namespace MarketNest.Admin.Infrastructure;

/// <summary>
///     Abstract EF Core configuration for all reference data entity types.
///     Concrete subclasses add table name (in <c>public</c> schema) and any extra columns.
/// </summary>
public abstract class ReferenceDataConfiguration<T> : IEntityTypeConfiguration<T>
    where T : ReferenceData
{
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SortOrder).IsRequired();
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
        builder.HasIndex(x => x.Code).IsUnique();

        // Inactive records are filtered out from all EF queries by default.
        // Use IgnoreQueryFilters() to retrieve deactivated records when needed.
        builder.HasQueryFilter(x => x.IsActive);
    }
}

