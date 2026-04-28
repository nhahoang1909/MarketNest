using MarketNest.Admin.Domain;
using MarketNest.Base.Common;

namespace MarketNest.Admin.Infrastructure;

public class NationalityConfiguration : ReferenceDataConfiguration<Nationality>
{
    public override void Configure(EntityTypeBuilder<Nationality> builder)
    {
        builder.ToTable("nationalities", TableConstants.Schema.Default);
        base.Configure(builder);
    }
}

