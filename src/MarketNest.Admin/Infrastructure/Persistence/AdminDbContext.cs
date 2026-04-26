using Microsoft.EntityFrameworkCore;
using MarketNest.Core.Common.Persistence;
using MarketNest.Core.Common;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Infrastructure;

public class AdminDbContext : DbContext, IModuleDbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options) { }

    public string SchemaName => TableConstants.Schema.Admin;
    public string ContextName => "MarketNest.Admin";
    public DbContext AsDbContext() => this;

    public DbSet<TestEntity> Tests { get; set; } = null!;
    public DbSet<TestSubEntity> TestSubEntities { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(TableConstants.Schema.Admin);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AdminDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

