using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Infrastructure;

public class TestSubEntityConfiguration : IEntityTypeConfiguration<TestSubEntity>
{
    public void Configure(EntityTypeBuilder<TestSubEntity> builder)
    {
        builder.ToTable("TestSubEntities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ParentId).IsRequired();
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);

        builder.HasOne<TestEntity>()
            .WithMany("SubEntities")
            .HasForeignKey("ParentId")
            .HasPrincipalKey("Id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
