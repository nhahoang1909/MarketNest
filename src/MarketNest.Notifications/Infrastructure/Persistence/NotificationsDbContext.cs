using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;
using MarketNest.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Notifications.Infrastructure;

public class NotificationsDbContext : DbContext, IModuleDbContext
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : base(options)
    {
    }

    public DbSet<NotificationTemplate> NotificationTemplates { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;

    public string SchemaName => TableConstants.Schema.Notifications;
    public string ContextName => "MarketNest.Notifications";
    public DbContext AsDbContext() => this;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(TableConstants.Schema.Notifications);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
        modelBuilder.ApplyDddPropertyAccessConventions();
        base.OnModelCreating(modelBuilder);
    }
}

