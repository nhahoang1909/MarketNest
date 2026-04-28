using MarketNest.Base.Common;
using MarketNest.Promotions.Domain;

namespace MarketNest.Promotions.Infrastructure;

public class PromotionsReadDbContext(DbContextOptions<PromotionsReadDbContext> options) : DbContext(options)
{
    public DbSet<Voucher> Vouchers { get; set; } = null!;
    public DbSet<VoucherUsage> VoucherUsages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(TableConstants.Schema.Promotions);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PromotionsDbContext).Assembly);
        modelBuilder.ApplyDddPropertyAccessConventions();
        base.OnModelCreating(modelBuilder);
    }
}
