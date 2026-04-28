using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;
using MarketNest.Payments.Domain;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Payments.Infrastructure;

public class PaymentsDbContext : DbContext, IModuleDbContext
{
    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : base(options) { }

    public DbSet<CommissionPolicy> CommissionPolicies { get; set; } = null!;

    public string SchemaName => TableConstants.Schema.Payments;
    public string ContextName => "MarketNest.Payments";
    public DbContext AsDbContext() => this;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(TableConstants.Schema.Payments);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentsDbContext).Assembly);
        modelBuilder.ApplyDddPropertyAccessConventions();
        base.OnModelCreating(modelBuilder);
    }
}

