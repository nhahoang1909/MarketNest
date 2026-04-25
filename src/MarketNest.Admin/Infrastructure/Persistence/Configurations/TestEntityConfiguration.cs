using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Infrastructure;

public class TestEntityConfiguration : IEntityTypeConfiguration<TestEntity>
{
    public void Configure(EntityTypeBuilder<TestEntity> builder)
    {
        builder.ToTable("Tests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);

        builder.OwnsOne(m => m.Value, vo =>
        {
            vo.Property(v => v.Code).HasColumnName("Value_Code").HasMaxLength(50);
            vo.Property(v => v.Amount).HasColumnName("Value_Amount");
        });

        builder.Navigation(x => x.SubEntities).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
