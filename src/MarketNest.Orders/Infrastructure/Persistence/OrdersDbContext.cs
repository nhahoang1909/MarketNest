using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;
using MarketNest.Orders.Domain;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Orders.Infrastructure;

/// <summary>
///     Minimal write-side DbContext for the Orders module.
///     Phase 1: only contains <see cref="OrderPolicyConfig" /> for the business config table.
///     Full order aggregate tables will be added in later sprints.
/// </summary>
public class OrdersDbContext : DbContext, IModuleDbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }

    public DbSet<OrderPolicyConfig> OrderPolicyConfigs { get; set; } = null!;

    public string SchemaName => TableConstants.Schema.Orders;
    public string ContextName => "MarketNest.Orders";
    public DbContext AsDbContext() => this;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(TableConstants.Schema.Orders);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);
        modelBuilder.ApplyDddPropertyAccessConventions();
        base.OnModelCreating(modelBuilder);
    }
}

