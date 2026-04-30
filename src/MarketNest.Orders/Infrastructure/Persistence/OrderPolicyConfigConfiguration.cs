using MarketNest.Orders.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarketNest.Orders.Infrastructure;

public class OrderPolicyConfigConfiguration : IEntityTypeConfiguration<OrderPolicyConfig>
{
    public void Configure(EntityTypeBuilder<OrderPolicyConfig> builder)
    {
        builder.ToTable("order_policy_config");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever(); // singleton row, Id=1
        builder.Property(x => x.SellerConfirmWindowHours).IsRequired();
        builder.Property(x => x.AutoDeliverAfterShippedDays).IsRequired();
        builder.Property(x => x.AutoCompleteAfterDeliveredDays).IsRequired();
        builder.Property(x => x.DisputeWindowAfterDeliveredDays).IsRequired();
        builder.Property(x => x.UpdatedByAdminId);
        builder.Property(x => x.UpdatedAt).IsRequired();
    }
}

