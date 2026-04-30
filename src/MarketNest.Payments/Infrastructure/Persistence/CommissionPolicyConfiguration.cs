using MarketNest.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarketNest.Payments.Infrastructure;

public class CommissionPolicyConfiguration : IEntityTypeConfiguration<CommissionPolicy>
{
    public void Configure(EntityTypeBuilder<CommissionPolicy> builder)
    {
        builder.ToTable("commission_policies");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Rate).HasPrecision(5, 4).IsRequired();
        builder.Property(x => x.StorefrontId);
        builder.Property(x => x.EffectiveFrom).IsRequired();
        builder.Property(x => x.SetByAdminId).IsRequired();

        // Index for fast latest-rate lookup: (storefront_id ASC, effective_from DESC)
        builder.HasIndex(x => new { x.StorefrontId, x.EffectiveFrom });
    }
}

