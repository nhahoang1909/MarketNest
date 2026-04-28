using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;
using MarketNest.Promotions.Domain;

namespace MarketNest.Promotions.Infrastructure;

public class PromotionsDbContext : DbContext, IModuleDbContext
{
    public PromotionsDbContext(DbContextOptions<PromotionsDbContext> options) : base(options) { }

    public DbSet<Voucher> Vouchers { get; set; } = null!;
    public DbSet<VoucherUsage> VoucherUsages { get; set; } = null!;

    public string SchemaName => TableConstants.Schema.Promotions;
    public string ContextName => "MarketNest.Promotions";
    public DbContext AsDbContext() => this;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(TableConstants.Schema.Promotions);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PromotionsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
