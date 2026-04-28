using MarketNest.Promotions.Domain;

namespace MarketNest.Promotions.Infrastructure;

public class VoucherUsageConfiguration : IEntityTypeConfiguration<VoucherUsage>
{
    public void Configure(EntityTypeBuilder<VoucherUsage> builder)
    {
        builder.ToTable("voucher_usages");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("usage_id");

        builder.Property(u => u.VoucherId).HasColumnName("voucher_id").IsRequired();
        builder.Property(u => u.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(u => u.UserId).HasColumnName("user_id").IsRequired();

        builder.Property(u => u.DiscountApplied)
            .HasConversion(m => m.Amount, a => new Money(a, DomainConstants.Currencies.Default))
            .HasColumnName("discount_applied")
            .HasColumnType("numeric(10,2)")
            .IsRequired();

        builder.Property(u => u.UsedAt).HasColumnName("used_at").IsRequired();

        builder.HasIndex(u => new { u.VoucherId, u.OrderId }).IsUnique();
        builder.HasIndex(u => new { u.VoucherId, u.UserId });
    }
}
