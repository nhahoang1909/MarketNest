using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;
using MarketNest.Catalog.Domain;

namespace MarketNest.Catalog.Infrastructure;

public class CatalogDbContext : DbContext, IModuleDbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<ProductVariant> Variants { get; set; } = null!;

    public string SchemaName => TableConstants.Schema.Catalog;
    public string ContextName => "MarketNest.Catalog";
    public DbContext AsDbContext() => this;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(TableConstants.Schema.Catalog);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
        modelBuilder.ApplyDddPropertyAccessConventions();
        base.OnModelCreating(modelBuilder);
    }
}

