using MarketNest.Admin.Domain;
using MarketNest.Base.Common;

namespace MarketNest.Admin.Infrastructure;

public class CountryConfiguration : ReferenceDataConfiguration<Country>
{
    public override void Configure(EntityTypeBuilder<Country> builder)
    {
        // Override to public schema — reference data is shared across all modules
        builder.ToTable(TableConstants.ReferenceTable.Country, TableConstants.Schema.Default);
        base.Configure(builder);
        builder.Property(x => x.Iso3).HasMaxLength(3).IsRequired();
        builder.Property(x => x.FlagEmoji).HasMaxLength(10);
    }
}

