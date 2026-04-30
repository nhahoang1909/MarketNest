using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;
using MarketNest.Base.Utility;
using MarketNest.Notifications.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarketNest.Notifications.Infrastructure;

/// <summary>DI registration for the Notifications module.</summary>
public static class NotificationsServiceExtensions
{
    private const string WriteConnectionName = "DefaultConnection";
    private const string ReadConnectionName = "ReadConnection";

    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string writeConnection = configuration.GetConnectionString(WriteConnectionName)
            ?? throw new InvalidOperationException(
                $"Connection string '{WriteConnectionName}' is not configured.");

        // ReadConnection falls back to DefaultConnection when empty — Phase 2: point to read replica
        string readConnection = configuration.GetConnectionString(ReadConnectionName)
                                    is { Length: > 0 } rc ? rc : writeConnection;

        // Write context — tracked by DatabaseInitializer for migrations
        services.AddDbContext<NotificationsDbContext>(opts => opts.UseNpgsql(writeConnection));
        services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<NotificationsDbContext>());

        // Read context — NoTracking, no migrations; uses read replica when ReadConnection is set
        services.AddDbContext<NotificationsReadDbContext>(opts =>
            opts.UseNpgsql(readConnection)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        // SMTP options
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.Section));

        // Infrastructure services
        services.AddSingleton<ITemplateRenderer, HandlebarsTemplateRenderer>();
        services.AddSingleton<IEmailLayoutRenderer, EmailLayoutRenderer>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        // Repositories
        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();

        // Queries
        services.AddScoped<IGetNotificationInboxQuery, GetNotificationInboxQuery>();
        services.AddScoped<IGetUnreadCountQuery, UnreadCountQuery>();

        // Application service (cross-module contract implementation)
        services.AddScoped<INotificationService, NotificationService>();

        // Background job
        services.AddSingleton<IBackgroundJob, CleanupExpiredNotificationsJob>();

        return services;
    }
}

