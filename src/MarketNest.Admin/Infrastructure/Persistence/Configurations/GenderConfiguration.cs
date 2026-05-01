using MarketNest.Admin.Domain;
using MarketNest.Base.Common;

namespace MarketNest.Admin.Infrastructure;

public class GenderConfiguration : ReferenceDataConfiguration<Gender>
{
    public override void Configure(EntityTypeBuilder<Gender> builder)
    {
        builder.ToTable(TableConstants.ReferenceTable.Gender, TableConstants.Schema.Default);
        base.Configure(builder);
    }
}

