using Microsoft.EntityFrameworkCore;
using MarketNest.Admin.Domain;
using MarketNest.Core.Common;

namespace MarketNest.Admin.Infrastructure;

public class AdminReadDbContext(DbContextOptions<AdminReadDbContext> options) : DbContext(options)
{
    public DbSet<TestEntity> Tests { get; set; } = null!;
    public DbSet<TestSubEntity> TestSubEntities { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(TableConstants.Schema.Admin);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AdminDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
