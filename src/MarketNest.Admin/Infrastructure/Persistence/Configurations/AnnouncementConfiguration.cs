using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Infrastructure;

public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("Announcements");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(FieldLimits.InlineStandard.MaxLength);

        builder.Property(x => x.Message)
            .IsRequired()
            .HasMaxLength(FieldLimits.MultilineLong.MaxLength);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(FieldLimits.Identifier.MaxLength);

        builder.Property(x => x.LinkUrl)
            .HasMaxLength(FieldLimits.Url.MaxLength);

        builder.Property(x => x.LinkText)
            .HasMaxLength(FieldLimits.InlineShort.MaxLength);

        builder.Property(x => x.StartDateUtc).IsRequired();
        builder.Property(x => x.EndDateUtc).IsRequired();
        builder.Property(x => x.IsPublished).IsRequired();
        builder.Property(x => x.IsDismissible).IsRequired();
        builder.Property(x => x.SortOrder).IsRequired();

        // Index for active announcements query (published + date range)
        builder.HasIndex(x => new { x.IsPublished, x.StartDateUtc, x.EndDateUtc })
            .HasDatabaseName("IX_Announcements_Active");
    }
}

