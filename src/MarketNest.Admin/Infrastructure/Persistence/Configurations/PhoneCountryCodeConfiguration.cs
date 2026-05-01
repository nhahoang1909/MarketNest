using MarketNest.Admin.Domain;
using MarketNest.Base.Common;

namespace MarketNest.Admin.Infrastructure;

public class PhoneCountryCodeConfiguration : ReferenceDataConfiguration<PhoneCountryCode>
{
    public override void Configure(EntityTypeBuilder<PhoneCountryCode> builder)
    {
        builder.ToTable(TableConstants.ReferenceTable.PhoneCountryCode, TableConstants.Schema.Default);
        base.Configure(builder);
        builder.Property(x => x.DialCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.CountryCode).HasMaxLength(3).IsRequired();
    }
}

