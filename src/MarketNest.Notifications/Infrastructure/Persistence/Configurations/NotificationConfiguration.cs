using MarketNest.Notifications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarketNest.Notifications.Infrastructure;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable(TableConstants.NotificationTable.Notification, TableConstants.Schema.Notifications);

        builder.HasKey(n => n.Id);

        builder.Property(n => n.UserId)
            .IsRequired();

        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt })
            .HasDatabaseName("ix_notifications_user_unread");

        builder.Property(n => n.TemplateKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(n => n.Title)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(n => n.Body)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(n => n.ActionUrl)
            .HasMaxLength(500);

        builder.Property(n => n.IsRead)
            .HasDefaultValue(false);

        builder.Property(n => n.CreatedAt)
            .IsRequired();

        builder.Property(n => n.ExpiresAt)
            .IsRequired();

        builder.HasIndex(n => n.ExpiresAt)
            .HasDatabaseName("ix_notifications_expires");
    }
}

