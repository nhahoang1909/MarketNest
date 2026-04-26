using MarketNest.Admin.Domain;
using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Admin.Infrastructure;

public class AdminDbContext : DbContext, IModuleDbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options)
    {
    }

    public DbSet<TestEntity> Tests { get; set; } = null!;
    public DbSet<TestSubEntity> TestSubEntities { get; set; } = null!;

    public string SchemaName => TableConstants.Schema.Admin;
    public string ContextName => "MarketNest.Admin";
    public DbContext AsDbContext() => this;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(TableConstants.Schema.Admin);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AdminDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
