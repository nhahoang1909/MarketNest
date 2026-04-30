using MarketNest.Notifications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarketNest.Notifications.Infrastructure;

public class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("notification_templates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TemplateKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(t => t.TemplateKey)
            .IsUnique();

        builder.Property(t => t.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Channel)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.SubjectTemplate)
            .HasMaxLength(500);

        builder.Property(t => t.BodyTemplate)
            .IsRequired();

        builder.Property(t => t.AvailableVariables)
            .HasColumnType("text[]");

        builder.Property(t => t.IsActive)
            .HasDefaultValue(true);

        builder.Property(t => t.CreatedAt)
            .IsRequired();
    }
}

