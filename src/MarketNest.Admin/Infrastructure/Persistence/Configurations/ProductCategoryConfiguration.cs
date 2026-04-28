using MarketNest.Admin.Domain;
using MarketNest.Base.Common;

namespace MarketNest.Admin.Infrastructure;

public class ProductCategoryConfiguration : ReferenceDataConfiguration<ProductCategory>
{
    public override void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.ToTable("product_categories", TableConstants.Schema.Default);
        base.Configure(builder);
        builder.Property(x => x.Slug).HasMaxLength(60).IsRequired();
        builder.Property(x => x.IconName).HasMaxLength(50);
        builder.Property(x => x.ParentId);
        builder.HasIndex(x => x.Slug).IsUnique();

        // Self-referential FK (parent-child, max 2 levels — enforced at application layer)
        builder.HasOne<ProductCategory>()
            .WithMany()
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

