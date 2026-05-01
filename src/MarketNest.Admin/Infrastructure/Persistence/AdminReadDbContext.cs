using MarketNest.Admin.Domain;
using MarketNest.Base.Common;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Admin.Infrastructure;

public class AdminReadDbContext(DbContextOptions<AdminReadDbContext> options) : DbContext(options)
{

    // ── Announcements ───────────────────────────────────────────────────
    public DbSet<Announcement> Announcements { get; set; } = null!;

    // ── Tier 1 — Reference Data ──────────────────────────────────────────
    public DbSet<Country> Countries { get; set; } = null!;
    public DbSet<Gender> Genders { get; set; } = null!;
    public DbSet<PhoneCountryCode> PhoneCountryCodes { get; set; } = null!;
    public DbSet<Nationality> Nationalities { get; set; } = null!;
    public DbSet<ProductCategory> ProductCategories { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(TableConstants.Schema.Admin);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AdminDbContext).Assembly);
        modelBuilder.ApplyDddPropertyAccessConventions();
        base.OnModelCreating(modelBuilder);
    }
}
