using MarketNest.Promotions.Domain;

namespace MarketNest.Promotions.Infrastructure;

public class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        builder.ToTable("vouchers");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("voucher_id");

        builder.Property(v => v.Code)
            .HasConversion(c => c.Value, s => new VoucherCode(s))
            .HasColumnName("code")
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(v => v.Code).IsUnique();

        builder.Property(v => v.Scope).HasColumnName("scope").IsRequired();
        builder.Property(v => v.StoreId).HasColumnName("store_id");
        builder.Property(v => v.CreatedByUserId).HasColumnName("created_by").IsRequired();

        builder.Property(v => v.DiscountType).HasColumnName("discount_type").IsRequired();
        builder.Property(v => v.ApplyFor).HasColumnName("apply_for").IsRequired();
        builder.Property(v => v.DiscountValue).HasColumnName("discount_value").HasColumnType("numeric(10,4)").IsRequired();

        builder.Property(v => v.MaxDiscountCap)
            .HasConversion(m => m == null ? (decimal?)null : m.Amount, a => a.HasValue ? new Money(a.Value, DomainConstants.Currencies.Default) : null)
            .HasColumnName("max_discount_cap")
            .HasColumnType("numeric(10,2)");

        builder.Property(v => v.MinOrderValue)
            .HasConversion(m => m == null ? (decimal?)null : m.Amount, a => a.HasValue ? new Money(a.Value, DomainConstants.Currencies.Default) : null)
            .HasColumnName("min_order_value")
            .HasColumnType("numeric(10,2)");

        builder.Property(v => v.EffectiveDate).HasColumnName("effective_date").IsRequired();
        builder.Property(v => v.ExpiryDate).HasColumnName("expiry_date").IsRequired();

        builder.Property(v => v.UsageLimit).HasColumnName("usage_limit");
        builder.Property(v => v.UsageLimitPerUser).HasColumnName("usage_limit_per_user");
        builder.Property(v => v.UsageCount).HasColumnName("usage_count").HasDefaultValue(0).IsRequired();

        builder.Property(v => v.Status).HasColumnName("status").IsRequired();
        builder.Property(v => v.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(v => v.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasMany(v => v.Usages)
            .WithOne()
            .HasForeignKey(u => u.VoucherId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
