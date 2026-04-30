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

    // ── Test entities (scaffold) ─────────────────────────────────────────
    public DbSet<TestEntity> Tests { get; set; } = null!;
    public DbSet<TestSubEntity> TestSubEntities { get; set; } = null!;

    // ── Tier 1 — Reference Data (mapped to public schema) ────────────────
    public DbSet<Country> Countries { get; set; } = null!;
    public DbSet<Gender> Genders { get; set; } = null!;
    public DbSet<PhoneCountryCode> PhoneCountryCodes { get; set; } = null!;
    public DbSet<Nationality> Nationalities { get; set; } = null!;
    public DbSet<ProductCategory> ProductCategories { get; set; } = null!;

    public string SchemaName => TableConstants.Schema.Admin;
    public string ContextName => "MarketNest.Admin";
    public DbContext AsDbContext() => this;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(TableConstants.Schema.Admin);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AdminDbContext).Assembly);
        modelBuilder.ApplyDddPropertyAccessConventions();
        modelBuilder.ApplyConcurrencyTokenConventions();
        base.OnModelCreating(modelBuilder);
    }
}
