using MarketNest.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Notifications.Infrastructure;

public class NotificationsReadDbContext(DbContextOptions<NotificationsReadDbContext> options) : DbContext(options)
{
    public DbSet<NotificationTemplate> NotificationTemplates { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(MarketNest.Base.Common.TableConstants.Schema.Notifications);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
        modelBuilder.ApplyDddPropertyAccessConventions();
        base.OnModelCreating(modelBuilder);
    }
}

