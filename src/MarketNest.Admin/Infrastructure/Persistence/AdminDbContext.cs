using Microsoft.EntityFrameworkCore;
using MarketNest.Core.Common.Persistence;
using MarketNest.Core.Common;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Infrastructure;

public class AdminDbContext : DbContext, IModuleDbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options)
    {
    }

    public string SchemaName => TableConstants.Schema.Admin;
    public string ContextName => "MarketNest.Admin";
    public DbContext AsDbContext() => this;

    public DbSet<TestEntity> Tests { get; set; } = null!;
    public DbSet<TestSubEntity> TestSubEntities { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(TableConstants.Schema.Admin);

        modelBuilder.Entity<TestEntity>(b =>
        {
            b.ToTable("Tests");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);

            // Configure owned type explicitly using the CLR type
            b.OwnsOne(m => m.Value, vo =>
            {
                vo.Property(v => v.Code).HasColumnName("Value_Code").HasMaxLength(50);
                vo.Property(v => v.Amount).HasColumnName("Value_Amount");
            });

            b.Navigation(x => x.SubEntities).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<TestSubEntity>(b =>
        {
            b.ToTable("TestSubEntities");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ParentId).IsRequired();
            b.Property(x => x.Title).IsRequired().HasMaxLength(200);

            b.HasOne<TestEntity>()
                .WithMany("SubEntities")
                .HasForeignKey("ParentId")
                .HasPrincipalKey("Id")
                .OnDelete(DeleteBehavior.Cascade);
        });

        base.OnModelCreating(modelBuilder);
    }
}

